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
        private readonly IParserService parserService;
        private readonly IPersistenceService persistenceService;


        public DataService(
            IPersistenceService persistenceService,
            IParserService parserService)
        {
            this.persistenceService = persistenceService;
            this.parserService = parserService;
        }


        public void SaveData(IReadOnlyList<IDataItem> items, string dataFilePath) =>
            persistenceService.SerializeSave(items, dataFilePath);


        public Task<R> ParseAsync(string filePath, DataItemsCallback dataItemsCallback) =>
            parserService.ParseAsync(filePath, dataItemsCallback);


        public Task<R> TryReadSavedDataAsync(string dataFilePath, DataItemsCallback dataItemsCallback) =>
            persistenceService.TryDeserialize(dataFilePath, dataItemsCallback);


        public void CacheData(IReadOnlyList<IDataItem> items, string dataFilePath) =>
            persistenceService.SerializeCache(items, dataFilePath);
    }
}
