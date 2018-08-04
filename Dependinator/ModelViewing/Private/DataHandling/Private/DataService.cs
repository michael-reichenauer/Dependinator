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


        public void StartMonitorData(string filePath) => dataMonitorService.StartMonitorData(filePath);

        public void StopMonitorData() => dataMonitorService.StopMonitorData();

        public Task SaveAsync(IReadOnlyList<IDataItem> items) => persistenceService.SaveAsync(items);


        public Task<R> ParseAsync(string filePath, DataItemsCallback dataItemsCallback) =>
            parserService.ParseAsync(filePath, dataItemsCallback);


        public Task<R> TryReadSavedDataAsync(string dataFilePath, DataItemsCallback dataItemsCallback) =>
            persistenceService.TryDeserialize(dataFilePath, dataItemsCallback);
    }
}
