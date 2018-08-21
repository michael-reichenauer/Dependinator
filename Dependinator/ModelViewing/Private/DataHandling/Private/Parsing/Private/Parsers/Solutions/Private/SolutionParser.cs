using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Dependinator.ModelViewing.Private.DataHandling.Private.Parsing.Private.Parsers.Assemblies;
using Dependinator.Utils;
using Dependinator.Utils.ErrorHandling;


namespace Dependinator.ModelViewing.Private.DataHandling.Private.Parsing.Private.Parsers.Solutions.Private
{
    internal class SolutionParser : IDisposable
    {
        private static readonly char[] PartsSeparators = "./".ToCharArray();

        private readonly List<AssemblyParser> assemblyParsers = new List<AssemblyParser>();
        private readonly bool isReadSymbols;

        private readonly List<NodeData> parentNodesToSend = new List<NodeData>();
        private readonly string solutionFilePath;
        private readonly Action<NodeData> nodeCallback;
        private readonly Action<LinkData> linkCallback;


        public SolutionParser(
            string solutionFilePath,
            Action<NodeData> nodeCallback,
            Action<LinkData> linkCallback,
            bool isReadSymbols)
        {
            this.solutionFilePath = solutionFilePath;
            this.nodeCallback = nodeCallback;
            this.linkCallback = linkCallback;
            this.isReadSymbols = isReadSymbols;
        }

        private string SolutionNodeName => Path.GetFileName(solutionFilePath).Replace(".", "*");

        public void Dispose() => assemblyParsers.ForEach(parser => parser.Dispose());


        public async Task<M> ParseAsync()
        {
            parentNodesToSend.Add(CreateSolutionNode());

            M result = CreateAssemblyParsers();
            if (result.IsFaulted)
            {
                return result.Error;
            }

            parentNodesToSend.ForEach(node => nodeCallback(node));

            await ParseSolutionAssembliesAsync();
            int typeCount = assemblyParsers.Sum(parser => parser.TypeCount);
            int memberCount = assemblyParsers.Sum(parser => parser.MemberCount);
            int ilCount = assemblyParsers.Sum(parser => parser.IlCount);
            int linksCount = assemblyParsers.Sum(parser => parser.LinksCount);

            Log.Debug($"Solution: {typeCount} types, {memberCount} members, {ilCount} il-instructions, {linksCount} links");
            return M.Ok;
        }


        public async Task<M<NodeDataSource>> TryGetSourceAsync(string nodeName)
        {
            M result = CreateAssemblyParsers(true);

            if (result.IsFaulted)
            {
                return result.Error;
            }

            string moduleName = GetModuleName(nodeName);
            AssemblyParser assemblyParser = assemblyParsers
                .FirstOrDefault(p => p.ModuleName == moduleName);

            if (assemblyParser == null)
            {
                return Error.From($"Failed to find assembly for {moduleName}");
            }

            return await Task.Run(() =>
            {
                M<NodeDataSource> source = assemblyParser.TryGetSource(nodeName);

                if (source.IsFaulted || source.Value.Path != null) return source;

                var sourcePath = TryGetFilePath(nodeName, assemblyParser.ProjectPath);
                if (sourcePath.IsOk)
                {
                    return new NodeDataSource(source.Value.Text, source.Value.LineNumber, sourcePath.Value);
                }

                return source;
            });
        }



        public async Task<M<string>> TryGetNodeAsync(NodeDataSource source)
        {
            await Task.Yield();

            M result = CreateAssemblyParsers(true);

            if (result.IsFaulted)
            {
                return result.Error;
            }

            foreach (AssemblyParser parser in assemblyParsers)
            {
                if (parser.TryGetNode(source.Path, out string nodeName))
                {
                    return nodeName;
                }
            }

            string sourceFilePath = Path.GetDirectoryName(source.Path);
            foreach (AssemblyParser parser in assemblyParsers)
            {
                if (parser.TryGetNode(sourceFilePath, out string nodeName))
                {
                    return GetParentName(nodeName);
                }
            }

            return Error.From($"Failed to find node for {sourceFilePath}");
        }





        public static IReadOnlyList<string> GetDataFilePaths(string solutionFilePath)
        {
            Solution solution = new Solution(solutionFilePath);

            return solution.GetDataFilePaths();
        }


        private NodeData CreateSolutionNode() =>
            new NodeData(SolutionNodeName, null, NodeData.SolutionType, "Solution file");


        private M CreateAssemblyParsers(bool includeReferences = false)
        {
            string solutionName = SolutionNodeName;

            Solution solution = new Solution(solutionFilePath);
            IReadOnlyList<Project> projects = solution.GetSolutionProjects();

            foreach (Project project in projects)
            {
                string assemblyPath = project.GetOutputPath();
                if (assemblyPath == null)
                {
                    return Error.From($"Failed to parse:\n {solutionFilePath}\n" +
                        $"Project\n{project}\nhas no Debug assembly.");
                }

                string parent = GetProjectParentName(solutionName, project);

                var assemblyParser = new AssemblyParser(
                    assemblyPath, project.ProjectFilePath, parent, nodeCallback, linkCallback, isReadSymbols);

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
                    var assemblyParser = new AssemblyParser(
                        referencePath, null, null, nodeCallback, linkCallback, isReadSymbols);

                    assemblyParsers.Add(assemblyParser);
                }
            }

            return M.Ok;
        }


        private string GetProjectParentName(string solutionName, Project project)
        {
            string parent = solutionName;
            string projectName = project.ProjectFullName;

            string[] parts = projectName.Split("\\".ToCharArray());
            if (parts.Length == 1)
            {
                return parent;
            }

            for (int i = 0; i < parts.Length - 1; i++)
            {
                string name = string.Join(".", parts.Take(i + 1));
                string folderName = $"{solutionName}.{name}";

                if (!parentNodesToSend.Any(n => n.Name == folderName))
                {
                    NodeData folderNode = new NodeData(folderName, parent, NodeData.SolutionFolderType, null);
                    parentNodesToSend.Add(folderNode);
                }

                parent = folderName;
            }

            return parent;
        }


        private static string GetModuleName(string nodeName)
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


        private static string GetParentName(string nodeName)
        {
            // Split full name in name and parent name,
            var fullName = (string)nodeName;
            int index = fullName.LastIndexOfAny(PartsSeparators);

            return index > -1 ? fullName.Substring(0, index) : "";
        }


        private M<string> TryGetFilePath(string nodeName, string projectPath)
        {
            // Source information did not contain file path info. Try locate file within project
            string solutionFolderPath = Path.GetDirectoryName(projectPath ?? solutionFilePath);

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

            return M.NoValue;
        }


        private static string GetShortName(string nodeName)
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


        private async Task ParseSolutionAssembliesAsync()
        {
            ParallelOptions option = GetParallelOptions();

            var internalModules = assemblyParsers.Select(p => p.ModuleName).ToList();

            await Task.Run(() =>
            {
                Parallel.ForEach(assemblyParsers, option, parser => parser.ParseAssemblyModule());
                Parallel.ForEach(assemblyParsers, option, parser => parser.ParseAssemblyReferences(internalModules));
                Parallel.ForEach(assemblyParsers, option, parser => parser.ParseTypes());
                Parallel.ForEach(assemblyParsers, option, parser => parser.ParseTypeMembers());
            });
        }


        private static ParallelOptions GetParallelOptions()
        {
            // Leave room for UI thread
            int workerThreadsCount = Math.Max(Environment.ProcessorCount - 1, 1);

            // workerThreadsCount = 1;
            var option = new ParallelOptions { MaxDegreeOfParallelism = workerThreadsCount };
            Log.Debug($"Parallelism: {workerThreadsCount}");
            return option;
        }
    }
}
