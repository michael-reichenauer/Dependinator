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
        public async Task<M> ParseAsync(DataFile dataFile, DataItemsCallback itemsCallback)
        {
            Log.Debug($"Parse {dataFile} ...");
            Timing t = Timing.Start();

            M<WorkParser> workItemParser = new WorkParser(dataFile, itemsCallback);
            if (workItemParser.IsFaulted)
            {
                return workItemParser;
            }

            using (workItemParser.Value)
            {
                await workItemParser.Value.ParseAsync();
            }

            t.Log($"Parsed {dataFile}");
            return M.Ok;
        }


        public async Task<M<Source>> GetSourceAsync(DataFile dataFile, DataNodeName nodeName)
        {
            M<WorkParser> workItemParser = new WorkParser(dataFile, null);
            if (workItemParser.IsFaulted)
            {
                return workItemParser.Error;
            }

            using (workItemParser.Value)
            {
                return await workItemParser.Value.GetSourceAsync(nodeName);
            }
        }


        public async Task<M<DataNodeName>> GetNodeForFilePathAsync(DataFile dataFile, Source source)
        {
            M<WorkParser> workItemParser = new WorkParser(dataFile, null);
            if (workItemParser.IsFaulted)
            {
                return workItemParser.Error;
            }

            using (workItemParser.Value)
            {
                return await workItemParser.Value.GetNodeForFilePathAsync(source);
            }
        }


        public IReadOnlyList<string> GetDataFilePaths(DataFile dataFile) =>
            WorkParser.GetDataFilePaths(dataFile);
    }
}
