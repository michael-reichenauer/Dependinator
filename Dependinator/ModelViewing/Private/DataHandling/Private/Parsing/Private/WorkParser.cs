using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Dependinator.ModelViewing.Private.DataHandling.Dtos;
using Dependinator.ModelViewing.Private.DataHandling.Private.Parsing.Private.AssemblyParsing;
using Dependinator.ModelViewing.Private.DataHandling.Private.Parsing.Private.SolutionParsing;
using Dependinator.Utils.ErrorHandling;


namespace Dependinator.ModelViewing.Private.DataHandling.Private.Parsing.Private
{
    internal class WorkParser : IDisposable
    {
        private readonly string filePath;
        private readonly DataItemsCallback itemsCallback;
        private AssemblyParser assemblyParser;

        private SolutionParser solutionParser;


        public WorkParser(string filePath, DataItemsCallback itemsCallback)
        {
            this.filePath = filePath;
            this.itemsCallback = itemsCallback;
        }


        public void Dispose()
        {
            solutionParser?.Dispose();
            assemblyParser?.Dispose();
        }


        public async Task<R> ParseAsync()
        {
            if (SolutionParser.IsSolutionFile(filePath))
            {
                solutionParser = new SolutionParser(filePath, itemsCallback, false);
                return await solutionParser.ParseAsync();
            }

            assemblyParser = new AssemblyParser(filePath, DataNodeName.None, itemsCallback, false);
            return await assemblyParser.ParseAsync();
        }


        public async Task<R<string>> GetCodeAsync(NodeName nodeName)
        {
            await Task.Yield();

            if (SolutionParser.IsSolutionFile(filePath))
            {
                solutionParser = new SolutionParser(filePath, null, true);
                return await solutionParser.GetCodeAsync(nodeName);
            }

            assemblyParser = new AssemblyParser(filePath, DataNodeName.None, itemsCallback, true);
            return assemblyParser.GetCode(nodeName);
        }


        public async Task<R<SourceLocation>> GetSourceFilePath(NodeName nodeName)
        {
            await Task.Yield();

            if (SolutionParser.IsSolutionFile(filePath))
            {
                solutionParser = new SolutionParser(filePath, null, true);
                return await solutionParser.GetSourceFilePathAsync(nodeName);
            }

            assemblyParser = new AssemblyParser(filePath, DataNodeName.None, itemsCallback, true);
            return assemblyParser.GetSourceFilePath(nodeName);
        }


        public async Task<R<NodeName>> GetNodeForFilePathAsync(string sourceFilePath)
        {
            await Task.Yield();

            if (SolutionParser.IsSolutionFile(filePath))
            {
                solutionParser = new SolutionParser(filePath, null, true);
                return await solutionParser.GetNodeNameForFilePathAsync(sourceFilePath);
            }

            return Error.From("Source file only available for solution based models");
        }


        public static IReadOnlyList<string> GetDataFilePaths(string filePath)
        {
            if (SolutionParser.IsSolutionFile(filePath))
            {
                return SolutionParser.GetDataFilePaths(filePath);
            }

            return AssemblyParser.GetDataFilePaths(filePath);
        }


        public static IReadOnlyList<string> GetBuildFolderPaths(string filePath)
        {
            if (SolutionParser.IsSolutionFile(filePath))
            {
                return SolutionParser.GetBuildFolderPaths(filePath);
            }

            return AssemblyParser.GetBuildFolderPaths(filePath);
        }
    }
}
