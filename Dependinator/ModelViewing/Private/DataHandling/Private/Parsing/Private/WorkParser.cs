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
        private readonly DataFile dataFile;
        private readonly DataItemsCallback itemsCallback;
        private AssemblyParser assemblyParser;

        private SolutionParser solutionParser;


        public WorkParser(DataFile dataFile, DataItemsCallback itemsCallback)
        {
            this.dataFile = dataFile;
            this.itemsCallback = itemsCallback;
        }


        public void Dispose()
        {
            solutionParser?.Dispose();
            assemblyParser?.Dispose();
        }


        public async Task<M> ParseAsync()
        {
            if (SolutionParser.IsSolutionFile(dataFile))
            {
                solutionParser = new SolutionParser(dataFile, itemsCallback, false);
                return await solutionParser.ParseAsync();
            }

            assemblyParser = new AssemblyParser(dataFile.FilePath, DataNodeName.None, itemsCallback, false);
            return await assemblyParser.ParseAsync();
        }


        public async Task<M<string>> GetCodeAsync(NodeName nodeName)
        {
            await Task.Yield();

            if (SolutionParser.IsSolutionFile(dataFile))
            {
                solutionParser = new SolutionParser(dataFile, null, true);
                return await solutionParser.GetCodeAsync(nodeName);
            }

            assemblyParser = new AssemblyParser(dataFile.FilePath, DataNodeName.None, itemsCallback, true);
            return assemblyParser.GetCode(nodeName);
        }


        public async Task<M<SourceLocation>> GetSourceFilePath(NodeName nodeName)
        {
            await Task.Yield();

            if (SolutionParser.IsSolutionFile(dataFile))
            {
                solutionParser = new SolutionParser(dataFile, null, true);
                return await solutionParser.GetSourceFilePathAsync(nodeName);
            }

            assemblyParser = new AssemblyParser(dataFile.FilePath, DataNodeName.None, itemsCallback, true);
            return assemblyParser.GetSourceFilePath(nodeName);
        }


        public async Task<M<NodeName>> GetNodeForFilePathAsync(string sourceFilePath)
        {
            await Task.Yield();

            if (SolutionParser.IsSolutionFile(dataFile))
            {
                solutionParser = new SolutionParser(dataFile, null, true);
                return await solutionParser.GetNodeNameForFilePathAsync(sourceFilePath);
            }

            return Error.From("Source file only available for solution based models");
        }


        public static IReadOnlyList<string> GetDataFilePaths(DataFile dataFile)
        {
            if (SolutionParser.IsSolutionFile(dataFile))
            {
                return SolutionParser.GetDataFilePaths(dataFile.FilePath);
            }

            return AssemblyParser.GetDataFilePaths(dataFile.FilePath);
        }


        public static IReadOnlyList<string> GetBuildFolderPaths(DataFile dataFile)
        {
            if (SolutionParser.IsSolutionFile(dataFile))
            {
                return SolutionParser.GetBuildFolderPaths(dataFile.FilePath);
            }

            return AssemblyParser.GetBuildFolderPaths(dataFile.FilePath);
        }
    }
}
