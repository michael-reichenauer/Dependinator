using System.Threading.Channels;
using Dependinator.Parsing.Assemblies;

namespace Dependinator.Parsing.Solutions;

internal class SolutionParser : IDisposable
{
    static readonly char[] PartsSeparators = "./".ToCharArray();

    readonly List<AssemblyParser> assemblyParsers = new List<AssemblyParser>();
    readonly bool isReadSymbols;
    readonly IFileService fileService;
    readonly List<Node> parentNodesToSend = new List<Node>();
    readonly string solutionFilePath;
    readonly ChannelWriter<IItem> items;


    public SolutionParser(
        string solutionFilePath,
        ChannelWriter<IItem> items,
        bool isReadSymbols,
        IFileService fileService)
    {
        this.solutionFilePath = solutionFilePath;
        this.items = items;
        this.isReadSymbols = isReadSymbols;
        this.fileService = fileService;
    }

    private string SolutionNodeName => Path.GetFileName(solutionFilePath).Replace(".", "*");

    public void Dispose() => assemblyParsers.ForEach(parser => parser.Dispose());


    public async Task<R> ParseAsync()
    {
        Log.Info("Parsing solution");
        parentNodesToSend.Add(CreateSolutionNode());

        if (!Try(out var e, CreateAssemblyParsers())) return e;
        //Log.Debug($"Solution: {assemblyParsers.Count} assemblies");

        parentNodesToSend.ForEach(async node => await items.WriteAsync(node));

        await ParseSolutionAssembliesAsync();
        int typeCount = assemblyParsers.Sum(parser => parser.TypeCount);
        int memberCount = assemblyParsers.Sum(parser => parser.MemberCount);
        int ilCount = assemblyParsers.Sum(parser => parser.IlCount);
        int linksCount = assemblyParsers.Sum(parser => parser.LinksCount);

        Log.Debug($"Solution: {typeCount} types, {memberCount} members, {ilCount} il-instructions, {linksCount} links");
        return R.Ok;
    }


    public async Task<R<Source>> TryGetSourceAsync(string nodeName)
    {
        if (!Try(out var e, CreateAssemblyParsers(true))) return e;

        string moduleName = GetModuleName(nodeName) ?? "";
        AssemblyParser? assemblyParser = assemblyParsers
            .FirstOrDefault(p => p.ModuleName == moduleName);

        if (assemblyParser == null) return R.Error($"Failed to find assembly for {moduleName}");


        return await Task.Run<R<Source>>(() =>
        {
            if (!Try(out var source, out var e, assemblyParser.TryGetSource(nodeName))) return e;
            if (!Try(out var path, out e, TryGetFilePath(nodeName, assemblyParser.ProjectPath))) return e;

            return new Source(path, source.Text, source.LineNumber);
        });
    }


    public async Task<R<string>> TryGetNodeAsync(Source source)
    {
        await Task.Yield();

        if (!Try(out var e, CreateAssemblyParsers(true))) return e;

        foreach (AssemblyParser parser in assemblyParsers)
        {
            if (parser.TryGetNode(source.Path, out string nodeName))
            {
                return nodeName;
            }
        }

        string sourceFilePath = Path.GetDirectoryName(source.Path) ?? "";
        foreach (AssemblyParser parser in assemblyParsers)
        {
            if (parser.TryGetNode(sourceFilePath, out string nodeName))
            {
                return GetParentName(nodeName);
            }
        }

        return R.Error($"Failed to find node for {sourceFilePath}");
    }


    public static IReadOnlyList<string> GetDataFilePaths(string solutionFilePath)
    {
        Solution solution = new Solution(solutionFilePath);

        return solution.GetDataFilePaths();
    }


    Node CreateSolutionNode() => new Node(SolutionNodeName, "", NodeType.Solution, "Solution file");


    R CreateAssemblyParsers(bool includeReferences = false)
    {
        string solutionName = SolutionNodeName;

        Solution solution = new Solution(solutionFilePath);
        IReadOnlyList<Project> projects = solution.GetSolutionProjects();

        foreach (Project project in projects)
        {
            string assemblyPath = project.GetOutputPath();
            if (string.IsNullOrEmpty(assemblyPath)) continue;
            // if (string.IsNullOrEmpty(assemblyPath)) return R.Error($"Failed to parse:\n {solutionFilePath}\n" +
            //     $"Project\n{project}\nhas no Debug assembly.");

            string parent = GetProjectParentName(solutionName, project);

            var assemblyParser = new AssemblyParser(
                assemblyPath, project.ProjectFilePath, parent, items, isReadSymbols, fileService);

            assemblyParsers.Add(assemblyParser);
        }

        if (includeReferences)
        {
            var internalModules = assemblyParsers.Select(p => p.ModuleName).ToList();
            var referencePaths = assemblyParsers
                .SelectMany(parser => parser.GetReferencePaths(internalModules))
                .Distinct()
                .Where(File.Exists)
                .ToList();

            foreach (string referencePath in referencePaths)
            {
                var assemblyParser = new AssemblyParser(referencePath, "", "", items, isReadSymbols, fileService);

                assemblyParsers.Add(assemblyParser);
            }
        }

        return R.Ok;
    }


    string GetProjectParentName(string solutionName, Project project)
    {
        string parentName = solutionName;
        string projectName = project.ProjectName;

        string[] parts = projectName.Split("\\".ToCharArray());
        if (parts.Length == 1)
        {
            return parentName;
        }

        for (int i = 0; i < parts.Length - 1; i++)
        {
            string name = string.Join(".", parts.Take(i + 1));
            string folderName = $"{solutionName}.{name}";

            if (!parentNodesToSend.Any(n => n.Name == folderName))
            {
                var folderNode = new Node(folderName, parentName, NodeType.SolutionFolder, "");
                parentNodesToSend.Add(folderNode);
            }

            parentName = folderName;
        }

        return parentName;
    }


    static string GetModuleName(string nodeName)
    {
        while (true)
        {
            string parentName = GetParentName(nodeName);
            if (parentName == "")
            {
                return nodeName;
            }

            nodeName = parentName;
        }
    }


    static string GetParentName(string nodeName)
    {
        // Split full name in name and parent name,
        var fullName = (string)nodeName;
        int index = fullName.LastIndexOfAny(PartsSeparators);

        return index > -1 ? fullName.Substring(0, index) : "";
    }


    R<string> TryGetFilePath(string nodeName, string projectPath)
    {
        // Source information did not contain file path info. Try locate file within project
        string solutionFolderPath = Path.GetDirectoryName(projectPath ?? solutionFilePath) ?? "";

        var filePaths = Directory
            .EnumerateFiles(solutionFolderPath, $"{GetShortName(nodeName)}.cs", SearchOption.AllDirectories)
            .ToList();

        if (filePaths.Count == 0)
        {
            // Name was not found, try with parent in case it is a member name
            filePaths = Directory
                .EnumerateFiles(solutionFolderPath, $"{GetShortName(GetParentName(nodeName))}.cs", SearchOption.AllDirectories)
                .ToList();
        }

        if (filePaths.Count == 1)
        {
            return filePaths[0];
        }

        return R.Error($"Failed to find file for {nodeName}");
    }


    static string GetShortName(string nodeName)
    {

        var fullName = (string)nodeName;

        int parametersIndex = fullName.IndexOf('(');
        if (parametersIndex > -1)
        {
            fullName = fullName.Substring(0, parametersIndex);
        }

        int index = fullName.LastIndexOfAny(PartsSeparators);

        return index > -1 ? fullName.Substring(index + 1) : "";
    }


    async Task ParseSolutionAssembliesAsync()
    {
        ParallelOptions option = GetParallelOptions();

        var internalModules = assemblyParsers.Select(p => p.ModuleName).ToList();
        // Log.Debug($"Solution: {internalModules.Count} internal modules:\n  {string.Join("\n  ", internalModules)}");

        await Task.Run(() =>
        {
            Parallel.ForEach(assemblyParsers, option, async parser => await parser.ParseAssemblyModuleAsync());
            Parallel.ForEach(assemblyParsers, option, async parser => await parser.ParseAssemblyReferencesAsync(internalModules));
            Parallel.ForEach(assemblyParsers, option, parser => parser.ParseTypes());
            Parallel.ForEach(assemblyParsers, option, async parser => await parser.ParseTypeMembersAsync());
        });
    }


    static ParallelOptions GetParallelOptions()
    {
        // Leave room for UI thread
        int workerThreadsCount = Math.Max(Environment.ProcessorCount - 1, 1);

        // workerThreadsCount = 1;
        var option = new ParallelOptions { MaxDegreeOfParallelism = workerThreadsCount };
        // Log.Debug($"Parallelism: {workerThreadsCount}");
        return option;
    }
}

