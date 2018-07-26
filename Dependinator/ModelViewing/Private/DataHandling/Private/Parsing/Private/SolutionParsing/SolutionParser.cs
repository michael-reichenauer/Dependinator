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
        private readonly List<AssemblyParser> assemblyParsers = new List<AssemblyParser>();
        private readonly bool isReadSymbols;
        private readonly DataItemsCallback itemsCallback;
        private readonly List<DataNode> parentNodesToSend = new List<DataNode>();
        private readonly string solutionFilePath;


        public SolutionParser(
            string solutionFilePath,
            DataItemsCallback itemsCallback,
            bool isReadSymbols)
        {
            this.solutionFilePath = solutionFilePath;
            this.itemsCallback = itemsCallback;
            this.isReadSymbols = isReadSymbols;
        }


        public void Dispose()
        {
            foreach (AssemblyParser parser in assemblyParsers)
            {
                parser.Dispose();
            }
        }


        public static bool IsSolutionFile(string filePath) =>
            Path.GetExtension(filePath).IsSameIgnoreCase(".sln");


        public async Task<R> ParseAsync()
        {
            parentNodesToSend.Add(GetSolutionNode());

            R result = CreateAssemblyParsers();
            if (result.IsFaulted)
            {
                return result.Error;
            }

            parentNodesToSend.ForEach(node => itemsCallback(node));

            await ParseSolutionAssembliesAsync();
            return R.Ok;
        }


        public async Task<R<string>> GetCodeAsync(NodeName nodeName)
        {
            await Task.Yield();

            R result = CreateAssemblyParsers();

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


        public async Task<R<SourceLocation>> GetSourceFilePathAsync(NodeName nodeName)
        {
            await Task.Yield();

            R result = CreateAssemblyParsers();

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


        public async Task<R<NodeName>> GetNodeNameForFilePathAsync(string sourceFilePath)
        {
            await Task.Yield();

            R result = CreateAssemblyParsers();

            if (result.IsFaulted)
            {
                return result.Error;
            }

            foreach (AssemblyParser parser in assemblyParsers)
            {
                if (parser.TryGetNodeNameFor(sourceFilePath, out NodeName nodeName))
                {
                    return nodeName;
                }
            }

            sourceFilePath = Path.GetDirectoryName(sourceFilePath);
            foreach (AssemblyParser parser in assemblyParsers)
            {
                if (parser.TryGetNodeNameFor(sourceFilePath, out NodeName nodeName))
                {
                    return nodeName.ParentName;
                }
            }

            return Error.From($"Failed to find node for {sourceFilePath}");
        }


        private DataNodeName GetSolutionNodeName()
        {
            string solutionName = Path.GetFileName(solutionFilePath).Replace(".", "*");
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
                {Description = "Solution file"};
            return solutionNode;
        }


        private R CreateAssemblyParsers()
        {
            DataNodeName solutionName = GetSolutionNodeName();

            Solution solution = new Solution(solutionFilePath);
            IReadOnlyList<Project> projects = solution.GetSolutionProjects();

            foreach (Project project in projects)
            {
                string assemblyPath = project.GetOutputPath();
                if (assemblyPath == null)
                {
                    return Error.From(new MissingAssembliesException(
                        $"Failed to parse:\n {solutionFilePath}\nProject\n{project}\nhas no Debug assembly."));
                }

                DataNodeName parent = GetParent(solutionName, project);

                var assemblyParser = new AssemblyParser(assemblyPath, parent, itemsCallback, isReadSymbols);

                assemblyParsers.Add(assemblyParser);
            }


            return R.Ok;
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


        private static string GetModuleName(NodeName nodeName)
        {
            while (true)
            {
                if (nodeName.ParentName == NodeName.Root)
                {
                    return nodeName.DisplayShortName;
                }

                nodeName = nodeName.ParentName;
            }
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
            var option = new ParallelOptions {MaxDegreeOfParallelism = workerThreadsCount};
            Log.Debug($"Parallelism: {workerThreadsCount}");
            return option;
        }
    }
}
