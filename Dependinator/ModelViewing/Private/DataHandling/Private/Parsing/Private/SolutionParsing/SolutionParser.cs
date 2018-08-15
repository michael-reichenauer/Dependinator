using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Dependinator.ModelViewing.Private.DataHandling.Dtos;
using Dependinator.ModelViewing.Private.DataHandling.Private.Parsing.Private.AssemblyParsing;
using Dependinator.ModelViewing.Private.DataHandling.Private.Parsing.Private.SolutionParsing.Private;
using Dependinator.Utils;
using Dependinator.Utils.ErrorHandling;


namespace Dependinator.ModelViewing.Private.DataHandling.Private.Parsing.Private.SolutionParsing
{
    internal class SolutionParser : IDisposable
    {
        private static readonly char[] PartsSeparators = "./".ToCharArray();

        private readonly List<AssemblyParser> assemblyParsers = new List<AssemblyParser>();
        private readonly bool isReadSymbols;
        private readonly DataItemsCallback itemsCallback;
        private readonly List<DataNode> parentNodesToSend = new List<DataNode>();
        private readonly DataFile dataFile;


        public SolutionParser(
            DataFile dataFile,
            DataItemsCallback itemsCallback,
            bool isReadSymbols)
        {
            this.dataFile = dataFile;
            this.itemsCallback = itemsCallback;
            this.isReadSymbols = isReadSymbols;
        }


        public void Dispose() => assemblyParsers.ForEach(parser => parser.Dispose());


        public static bool IsSolutionFile(DataFile dataFile) =>
            Path.GetExtension(dataFile.FilePath).IsSameIgnoreCase(".sln");


        public async Task<M> ParseAsync()
        {
            parentNodesToSend.Add(GetSolutionNode());

            M result = CreateAssemblyParsers();
            if (result.IsFaulted)
            {
                return result.Error;
            }

            parentNodesToSend.ForEach(node => itemsCallback(node));

            await ParseSolutionAssembliesAsync();
            int typeCount = assemblyParsers.Sum(parser => parser.TypeCount);
            int memberCount = assemblyParsers.Sum(parser => parser.MemberCount);
            int ilCount = assemblyParsers.Sum(parser => parser.IlCount);
            int linksCount = assemblyParsers.Sum(parser => parser.LinksCount);

            Log.Debug($"Solution: {typeCount} types, {memberCount} members, {ilCount} il-instructions, {linksCount} links");
            return M.Ok;
        }


        public async Task<M<string>> GetCodeAsync(DataNodeName nodeName)
        {
            await Task.Yield();

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

            return assemblyParser.GetCode(nodeName);
        }


        public async Task<M<Source>> GetSourceFilePathAsync(DataNodeName nodeName)
        {
            await Task.Yield();

            M result = CreateAssemblyParsers();

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

            return assemblyParser.GetSourceFilePath(nodeName);
        }


        public async Task<M<DataNodeName>> GetNodeNameForFilePathAsync(string sourceFilePath)
        {
            await Task.Yield();

            M result = CreateAssemblyParsers();

            if (result.IsFaulted)
            {
                return result.Error;
            }

            foreach (AssemblyParser parser in assemblyParsers)
            {
                if (parser.TryGetNodeNameFor(sourceFilePath, out DataNodeName nodeName))
                {
                    return nodeName;
                }
            }

            sourceFilePath = Path.GetDirectoryName(sourceFilePath);
            foreach (AssemblyParser parser in assemblyParsers)
            {
                if (parser.TryGetNodeNameFor(sourceFilePath, out DataNodeName nodeName))
                {
                    return GetParentName(nodeName);
                }
            }

            return Error.From($"Failed to find node for {sourceFilePath}");
        }


        private DataNodeName GetSolutionNodeName()
        {
            string solutionName = Path.GetFileName(dataFile.FilePath).Replace(".", "*");
            DataNodeName solutionNodeName = (DataNodeName)solutionName;
            return solutionNodeName;
        }


        public static IReadOnlyList<string> GetDataFilePaths(string filePath)
        {
            Solution solution = new Solution(filePath);

            return solution.GetDataFilePaths();
        }


        private DataNode GetSolutionNode()
        {
            DataNodeName solutionName = GetSolutionNodeName();
            DataNode solutionNode = new DataNode(solutionName, DataNodeName.None, NodeType.Solution)
            { Description = "Solution file" };
            return solutionNode;
        }


        private M CreateAssemblyParsers(bool includeReferences = false)
        {
            DataNodeName solutionName = GetSolutionNodeName();

            Solution solution = new Solution(dataFile.FilePath);
            IReadOnlyList<Project> projects = solution.GetSolutionProjects();

            foreach (Project project in projects)
            {
                string assemblyPath = project.GetOutputPath();
                if (assemblyPath == null)
                {
                    return Error.From(new MissingAssembliesException(
                        $"Failed to parse:\n {dataFile}\nProject\n{project}\nhas no Debug assembly."));
                }

                DataNodeName parent = GetParent(solutionName, project);

                var assemblyParser = new AssemblyParser(assemblyPath, parent, itemsCallback, isReadSymbols);

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
                    var assemblyParser = new AssemblyParser(referencePath, null, itemsCallback, isReadSymbols);

                    assemblyParsers.Add(assemblyParser);
                }
            }

            return M.Ok;
        }


        private DataNodeName GetParent(DataNodeName solutionName, Project project)
        {
            DataNodeName parent = solutionName;
            string projectName = project.ProjectFullName;

            string[] parts = projectName.Split("\\".ToCharArray());
            if (parts.Length == 1)
            {
                return parent;
            }

            for (int i = 0; i < parts.Length - 1; i++)
            {
                string name = string.Join(".", parts.Take(i + 1));
                DataNodeName folderName = (DataNodeName)$"{(string)solutionName}.{name}";

                if (!parentNodesToSend.Any(n => n.Name == folderName))
                {
                    DataNode folderNode = new DataNode(folderName, parent, NodeType.SolutionFolder);
                    parentNodesToSend.Add(folderNode);
                }

                parent = folderName;
            }

            return parent;
        }


        public static IReadOnlyList<string> GetBuildFolderPaths(string filePath)
        {
            Solution solution = new Solution(filePath);

            return solution.GetBuildFolderPaths();
        }


        private static string GetModuleName(DataNodeName nodeName)
        {
            while (true)
            {
                DataNodeName parentName = GetParentName(nodeName);
                if (parentName == DataNodeName.None)
                {
                    return (string)nodeName;
                }

                nodeName = parentName;
            }
        }


        private static DataNodeName GetParentName(DataNodeName nodeName)
        {
           
            // Split full name in name and parent name,
            var fullName = ((string)nodeName);
            int index = fullName.LastIndexOfAny(PartsSeparators);

            return index > -1 ? (DataNodeName)fullName.Substring(0, index) : DataNodeName.None;
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
