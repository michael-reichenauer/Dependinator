using System;
using System.Collections.Generic;
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
        private readonly IParserService parserService;
        private readonly IPersistenceService persistenceService;


        public DataService(
            IPersistenceService persistenceService,
            IParserService parserService,
            IDataMonitorService dataMonitorService)
        {
            this.persistenceService = persistenceService;
            this.parserService = parserService;
            this.dataMonitorService = dataMonitorService;
        }


        public event EventHandler DataChangedOccurred
        {
            add => dataMonitorService.DataChangedOccurred += value;
            remove => dataMonitorService.DataChangedOccurred -= value;
        }


        public void StartMonitorData(DataFile dataFile) => dataMonitorService.StartMonitorData(dataFile);

        public void StopMonitorData() => dataMonitorService.StopMonitorData();

        public Task SaveAsync(DataFile dataFile, IReadOnlyList<IDataItem> items) => 
            persistenceService.SaveAsync(dataFile, items);


        public Task<R> ParseAsync(DataFile dataFile, DataItemsCallback dataItemsCallback) =>
            parserService.ParseAsync(dataFile, dataItemsCallback);


        public async Task<R<string>> GetCodeAsync(DataFile dataFile, NodeName nodeName) => 
            await parserService.GetCodeAsync(dataFile, nodeName);


        public async Task<R<SourceLocation>> GetSourceFilePathAsync(DataFile dataFile, NodeName nodeName) => 
            await parserService.GetSourceFilePath(dataFile, nodeName);


        public async Task<R<NodeName>> GetNodeForFilePathAsync(DataFile dataFile, string sourceFilePath) => 
            await parserService.GetNodeForFilePathAsync(dataFile, sourceFilePath);


        public Task<R> TryReadSavedDataAsync(DataFile dataFile, DataItemsCallback dataItemsCallback) =>
            persistenceService.TryDeserialize(dataFile, dataItemsCallback);
    }
}
