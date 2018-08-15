using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Dependinator.ModelViewing.Private.DataHandling.Dtos;
using Dependinator.ModelViewing.Private.DataHandling.Private.Parsing;
using Dependinator.ModelViewing.Private.DataHandling.Private.Persistence;
using Dependinator.Utils.ErrorHandling;


namespace Dependinator.ModelViewing.Private.DataHandling.Private
{
    internal class DataService : IDataService
    {
        private readonly IDataMonitorService dataMonitorService;
        private readonly IDataFilePaths filePaths;
        private readonly IParserService parserService;
        private readonly IPersistenceService persistenceService;


        public DataService(
            IPersistenceService persistenceService,
            IParserService parserService,
            IDataMonitorService dataMonitorService,
            IDataFilePaths filePaths)
        {
            this.persistenceService = persistenceService;
            this.parserService = parserService;
            this.dataMonitorService = dataMonitorService;
            this.filePaths = filePaths;
        }


        public event EventHandler DataChangedOccurred
        {
            add => dataMonitorService.DataChangedOccurred += value;
            remove => dataMonitorService.DataChangedOccurred -= value;
        }


        public async Task<M> TryReadCacheAsync(DataFile dataFile, DataItemsCallback dataItemsCallback)
        {
            dataMonitorService.StartMonitorData(dataFile);

            M result = await persistenceService.TryReadCacheAsync(dataFile, dataItemsCallback);

            if (result.IsFaulted)
            {
                return result;
            }

            if (IsCacheOlderThanData(dataFile))
            {
                dataMonitorService.TriggerDataChanged();
            }

            return M.Ok;
        }


        public Task<M<IReadOnlyList<IDataItem>>> TryReadSaveAsync(DataFile dataFile) => 
            persistenceService.TryReadSaveAsync(dataFile);


        public Task<M> TryReadFreshAsync(DataFile dataFile, DataItemsCallback dataItemsCallback)
        {
            dataMonitorService.StartMonitorData(dataFile);
            return parserService.ParseAsync(dataFile, dataItemsCallback);
        }


        public Task SaveAsync(DataFile dataFile, IReadOnlyList<IDataItem> items)
        {
            return persistenceService.SaveAsync(dataFile, items);
        }


        public async Task<M<string>> GetCodeAsync(DataFile dataFile, DataNodeName nodeName) =>
            await parserService.GetCodeAsync(dataFile, nodeName);


        public async Task<M<Source>> GetSourceFilePathAsync(DataFile dataFile, DataNodeName nodeName) =>
            await parserService.GetSourceFilePath(dataFile, nodeName);


        public async Task<M<DataNodeName>> GetNodeForFilePathAsync(DataFile dataFile, string sourceFilePath) =>
            await parserService.GetNodeForFilePathAsync(dataFile, sourceFilePath);



        private bool IsCacheOlderThanData(DataFile dataFile)
        {
            IReadOnlyList<string> dataFilePaths = filePaths.GetDataFilePaths(dataFile);
            string cachePath = filePaths.GetCacheFilePath(dataFile);

            if (!File.Exists(cachePath))
            {
                return false;
            }


            DateTime cacheTime = File.GetLastWriteTime(cachePath);
            foreach (string dataFilePath in dataFilePaths)
            {
                if (File.Exists(dataFilePath))
                {
                    DateTime fileTime = File.GetLastWriteTime(dataFilePath);
                    if (fileTime > cacheTime)
                    {
                        return true;
                    }
                }
            }


            return false;
        }
    }
}
