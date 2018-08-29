using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Dependinator.Common;
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


        public async Task<M> TryReadCacheAsync(ModelPaths modelPaths, Action<IDataItem> dataItemsCallback)
        {
            parserService.StartMonitorDataChanges(modelPaths);

            M result = await persistenceService.TryReadCacheAsync(modelPaths, dataItemsCallback);

            if (result.IsFaulted)
            {
                return result;
            }

            return M.Ok;
        }


        public Task<M<IReadOnlyList<IDataItem>>> TryReadSaveAsync(ModelPaths modelPaths)
        {
            parserService.StartMonitorDataChanges(modelPaths);
            return persistenceService.TryReadSaveAsync(modelPaths);
        }


        public Task<M> TryReadFreshAsync(ModelPaths modelPaths, Action<IDataItem> dataItemsCallback)
        {
            parserService.StartMonitorDataChanges(modelPaths);
            return parserService.ParseAsync(modelPaths, dataItemsCallback);
        }


        public Task SaveAsync(ModelPaths modelPaths, IReadOnlyList<IDataItem> items)
        {
            return persistenceService.SaveAsync(modelPaths, items);
        }


        public async Task<M<Source>> TryGetSourceAsync(ModelPaths modelPaths, DataNodeName nodeName) =>
            await parserService.GetSourceAsync(modelPaths, nodeName);


        public async Task<M<DataNodeName>> TryGetNodeAsync(ModelPaths modelPaths, Source source) =>
            await parserService.TryGetNodeAsync(modelPaths, source);


        public void TriggerDataChangedIfDataNewerThanCache(ModelPaths modelPaths)
        {
            DateTime cacheTime = persistenceService.GetCacheTime(modelPaths);
            DateTime dataTime = parserService.GetDataTime(modelPaths);

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
