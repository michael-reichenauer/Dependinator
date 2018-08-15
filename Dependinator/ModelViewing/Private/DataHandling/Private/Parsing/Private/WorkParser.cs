using System;
using System.Collections.Generic;
using System.IO;
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

            DataNode workNode = GetWorkNode();
            itemsCallback(workNode);

            assemblyParser = new AssemblyParser(dataFile.FilePath, null, workNode.Name, itemsCallback, false);
            return await assemblyParser.ParseAsync();
        }


        public async Task<M<Source>> GetSourceAsync(DataNodeName nodeName)
        {
            await Task.Yield();

            if (SolutionParser.IsSolutionFile(dataFile))
            {
                solutionParser = new SolutionParser(dataFile, null, true);
                return await solutionParser.GetSourceAsync(nodeName);
            }

            assemblyParser = new AssemblyParser(dataFile.FilePath, null, DataNodeName.None, itemsCallback, true);
            return assemblyParser.TryGetSource(nodeName);
        }


        public async Task<M<DataNodeName>> GetNodeForFilePathAsync(Source source)
        {
            await Task.Yield();

            if (SolutionParser.IsSolutionFile(dataFile))
            {
                solutionParser = new SolutionParser(dataFile, null, true);
                return await solutionParser.GetNodeNameForFilePathAsync(source);
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


        private DataNode GetWorkNode()
        {
            DataNodeName solutionName = (DataNodeName)Path.GetFileName(dataFile.FilePath).Replace(".", "*");
            DataNode workNode = new DataNode(solutionName, DataNodeName.None, NodeType.Assembly)
                { Description = "Assembly file", Scale = 0.286 };
            return workNode;
        }
    }
}
