using System.Collections.Generic;
using System.Threading.Tasks;
using Dependinator.ModelViewing.Private.DataHandling.Dtos;
using Dependinator.Utils;
using Dependinator.Utils.ErrorHandling;
using Dependinator.Utils.Threading;


namespace Dependinator.ModelViewing.Private.DataHandling.Private.Parsing.Private
{
    internal class ParserService : IParserService
    {
        public async Task<R> ParseAsync(DataFile dataFile, DataItemsCallback itemsCallback)
        {
            Log.Debug($"Parse {dataFile} ...");
            Timing t = Timing.Start();

            R<WorkParser> workItemParser = new WorkParser(dataFile, itemsCallback);
            if (workItemParser.IsFaulted)
            {
                return workItemParser;
            }

            using (workItemParser.Value)
            {
                await workItemParser.Value.ParseAsync();
            }

            t.Log($"Parsed {dataFile}");
            return R.Ok;
        }


        public async Task<R<string>> GetCodeAsync(DataFile dataFile, NodeName nodeName)
        {
            R<WorkParser> workItemParser = new WorkParser(dataFile, null);
            if (workItemParser.IsFaulted)
            {
                return workItemParser.Error;
            }

            using (workItemParser.Value)
            {
                return await workItemParser.Value.GetCodeAsync(nodeName);
            }
        }


        public async Task<R<SourceLocation>> GetSourceFilePath(DataFile dataFile, NodeName nodeName)
        {
            R<WorkParser> workItemParser = new WorkParser(dataFile, null);
            if (workItemParser.IsFaulted)
            {
                return workItemParser.Error;
            }

            using (workItemParser.Value)
            {
                return await workItemParser.Value.GetSourceFilePath(nodeName);
            }
        }


        public async Task<R<NodeName>> GetNodeForFilePathAsync(DataFile dataFile, string sourceFilePath)
        {
            R<WorkParser> workItemParser = new WorkParser(dataFile, null);
            if (workItemParser.IsFaulted)
            {
                return workItemParser.Error;
            }

            using (workItemParser.Value)
            {
                return await workItemParser.Value.GetNodeForFilePathAsync(sourceFilePath);
            }
        }


        public IReadOnlyList<string> GetDataFilePaths(DataFile dataFile) =>
            WorkParser.GetDataFilePaths(dataFile);


        public IReadOnlyList<string> GetBuildPaths(DataFile dataFile) =>
            WorkParser.GetBuildFolderPaths(dataFile);
    }
}
