using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Dependinator.ModelViewing.Private.DataHandling.Dtos;
using Dependinator.ModelViewing.Private.DataHandling.Private.Parsing;
using Dependinator.ModelViewing.Private.DataHandling.Private.Persistence;
using Dependinator.Utils;
using Dependinator.Utils.Dependencies;
using Dependinator.Utils.ErrorHandling;


namespace Dependinator.ModelViewing.Private.DataHandling.Private
{
    [SingleInstance]
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
            parserService.DataChanged += (s, e) => DataChanged?.Invoke(this, e);
        }


        public event EventHandler DataChanged;


        public async Task<M> TryReadCacheAsync(DataFile dataFile, Action<IDataItem> dataItemsCallback)
        {
            parserService.StartMonitorDataChanges(dataFile);

            M result = await persistenceService.TryReadCacheAsync(dataFile, dataItemsCallback);

            if (result.IsFaulted)
            {
                return result;
            }

            return M.Ok;
        }


        public Task<M<IReadOnlyList<IDataItem>>> TryReadSaveAsync(DataFile dataFile)
        {
            parserService.StartMonitorDataChanges(dataFile);
            return persistenceService.TryReadSaveAsync(dataFile);
        }


        public Task<M> TryReadFreshAsync(DataFile dataFile, Action<IDataItem> dataItemsCallback)
        {
            parserService.StartMonitorDataChanges(dataFile);
            return parserService.ParseAsync(dataFile, dataItemsCallback);
        }


        public Task SaveAsync(DataFile dataFile, IReadOnlyList<IDataItem> items)
        {
            return persistenceService.SaveAsync(dataFile, items);
        }


        public async Task<M<Source>> TryGetSourceAsync(DataFile dataFile, DataNodeName nodeName) =>
            await parserService.GetSourceAsync(dataFile, nodeName);


        public async Task<M<DataNodeName>> TryGetNodeAsync(DataFile dataFile, Source source) =>
            await parserService.TryGetNodeAsync(dataFile, source);


        public void TriggerDataChangedIfDataNewerThanCache(DataFile dataFile)
        {
            DateTime cacheTime = persistenceService.GetCacheTime(dataFile);
            DateTime dataTime = parserService.GetDataTime(dataFile);

            Log.Debug($"Data time: {dataTime}, cache time: {cacheTime}");

            if (dataTime > cacheTime)
            {
                Log.Debug("Data is newer than cache");
                Task.Delay(TimeSpan.FromSeconds(5))
                    .ContinueWith(_ => DataChanged?.Invoke(this, EventArgs.Empty)).RunInBackground();
            }
        }
    }
}
