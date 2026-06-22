using Dependinator.Core.Parsing.Assemblies;
using Dependinator.Core.Parsing.Utils;
using Dependinator.Core.Shared;

namespace Dependinator.Core.Parsing.Solutions;

internal class SolutionParser : IDisposable
{
    static readonly char[] PartsSeparators = "./".ToCharArray();

    readonly List<AssemblyParser> assemblyParsers = new List<AssemblyParser>();
    readonly bool isReadSymbols;
    readonly IParserFileService fileService;
    readonly List<Node> parentNodesToSend = new List<Node>();

    readonly string solutionFilePath;
    readonly IItems items;

    public SolutionParser(string solutionFilePath, IItems items, bool isReadSymbols, IParserFileService fileService)
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
        MSBuildLocatorHelper.Register();
        Log.Info("Parsing solution", solutionFilePath);
        parentNodesToSend.Add(CreateSolutionNode());

        if (!Try(out var e, await CreateAssemblyParsersAsync()))
            return e;
        //Log.Debug($"Solution: {assemblyParsers.Count} assemblies");

        await parentNodesToSend.ForEachAsync(items.SendAsync);

        await ParseSolutionAssembliesAsync();
        int typeCount = assemblyParsers.Sum(parser => parser.TypeCount);
        int memberCount = assemblyParsers.Sum(parser => parser.MemberCount);
        int ilCount = assemblyParsers.Sum(parser => parser.IlCount);
        int linksCount = assemblyParsers.Sum(parser => parser.LinksCount);

        Log.Info($"Solution: {typeCount} types, {memberCount} members, {ilCount} il-instructions, {linksCount} links");
        return R.Ok;
    }

    public async Task<R<Source>> TryGetSourceAsync(string nodeName)
    {
        MSBuildLocatorHelper.Register();
        if (!Try(out var e, await CreateAssemblyParsersAsync(true)))
            return e;

        string moduleName = GetModuleName(nodeName) ?? "";

        AssemblyParser? assemblyParser = null;
        foreach (var parser in assemblyParsers)
        {
            var parserModuleName = parser.ModuleName;
            if (parserModuleName == moduleName)
            {
                assemblyParser = parser;
                break;
            }
        }

        if (assemblyParser == null)
            return R.Error($"Failed to find assembly for {moduleName}");

        return await Task.Run<R<Source>>(() =>
        {
            if (!Try(out var source, out var e, assemblyParser.TryGetSource(nodeName)))
                return e;

            return source;
        });
    }

    public async Task<R<string>> TryGetNodeAsync(FileLocation fileLocation)
    {
        MSBuildLocatorHelper.Register();
        await Task.Yield();

        if (!Try(out var e, await CreateAssemblyParsersAsync(true)))
            return e;

        foreach (AssemblyParser parser in assemblyParsers)
        {
            if (Try(out var nodeName, parser.TryGetNode(fileLocation)))
                return nodeName;
        }

        string sourceFilePath = Path.GetDirectoryName(fileLocation.Path) ?? "";
        foreach (AssemblyParser parser in assemblyParsers)
        {
            if (Try(out var nodeName, parser.TryGetNode(new FileLocation(sourceFilePath, fileLocation.Line))))
                return GetParentName(nodeName);
        }

        return R.Error($"Failed to find node for {sourceFilePath}");
    }

    public static IReadOnlyList<string> GetDataFilePaths(string solutionFilePath)
    {
        MSBuildLocatorHelper.Register();
        Solution solution = new Solution(solutionFilePath);

        return solution.GetDataFilePaths();
    }

    Node CreateSolutionNode() =>
        new(SolutionNodeName, new() { Type = NodeType.Solution, Description = "Solution file" });

    async Task<R> CreateAssemblyParsersAsync(bool includeReferences = false)
    {
        string solutionName = SolutionNodeName;
        Solution solution = new Solution(solutionFilePath);
        IReadOnlyList<Project> projects = solution.GetSolutionProjects();
        foreach (Project project in projects)
        {
            string assemblyPath = project.GetOutputPath();
            if (string.IsNullOrEmpty(assemblyPath))
                continue;
            // if (string.IsNullOrEmpty(assemblyPath)) return R.Error($"Failed to parse:\n {solutionFilePath}\n" +
            //     $"Project\n{project}\nhas no Debug assembly.");

            string parent = GetProjectParentName(solutionName, project);

            if (
                !Try(
                    out var assemblyParser,
                    out var e,
                    await AssemblyParser.CreateAsync(
                        assemblyPath,
                        project.ProjectFilePath,
                        parent,
                        items,
                        isReadSymbols,
                        fileService
                    )
                )
            )
                continue;

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
                if (
                    !Try(
                        out var assemblyParser,
                        await AssemblyParser.CreateAsync(referencePath, "", "", items, isReadSymbols, fileService)
                    )
                )
                    continue;

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
                var folderNode = new Node(folderName, new() { Type = NodeType.SolutionFolder, Parent = parentName });
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
                .EnumerateFiles(
                    solutionFolderPath,
                    $"{GetShortName(GetParentName(nodeName))}.cs",
                    SearchOption.AllDirectories
                )
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
        var internalModules = assemblyParsers.Select(p => p.ModuleName).ToList();

        await Task.WhenAll(assemblyParsers.Select(parser => parser.ParseAssemblyModuleAsync()));
        await Task.WhenAll(assemblyParsers.Select(parser => parser.ParseAssemblyReferencesAsync(internalModules)));
        await Task.WhenAll(assemblyParsers.Select(parser => parser.ParseTypesAsync()));
        await Task.WhenAll(assemblyParsers.Select(parser => parser.ParseTypeMembersAsync()));
    }
}
