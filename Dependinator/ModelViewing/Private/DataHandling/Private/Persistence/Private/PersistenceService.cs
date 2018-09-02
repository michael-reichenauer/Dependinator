using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Dependinator.Common;
using Dependinator.ModelViewing.Private.DataHandling.Dtos;
using Dependinator.ModelViewing.Private.DataHandling.Private.Parsing;
using Dependinator.Utils;
using Dependinator.Utils.ErrorHandling;
using Dependinator.Utils.Threading;


namespace Dependinator.ModelViewing.Private.DataHandling.Private.Persistence.Private
{
    internal class PersistenceService : IPersistenceService
    {
        private readonly ICacheSerializer cacheSerializer;
        private readonly ISaveSerializer saveSerializer;
        private readonly IParserService parserService;


        public PersistenceService(
            ICacheSerializer cacheSerializer,
            ISaveSerializer saveSerializer,
            IParserService parserService)
        {
            this.cacheSerializer = cacheSerializer;
            this.saveSerializer = saveSerializer;
            this.parserService = parserService;
        }


        public DateTime GetCacheTime(ModelPaths modelPaths)
        {
            string cacheFilePath = GetCacheFilePath(modelPaths);
            if (!File.Exists(cacheFilePath))
            {
                return DateTime.MinValue;
            }

            return File.GetLastWriteTime(cacheFilePath);
        }


        public DateTime GetSaveTime(ModelPaths modelPaths)
        {
            string saveFilePath = GetSaveFilePath(modelPaths);
            if (!File.Exists(saveFilePath))
            {
                return DateTime.MinValue;
            }

            return File.GetLastWriteTime(saveFilePath);
        }


        public async Task<M> TryReadCacheAsync(ModelPaths modelPaths, Action<IDataItem> dataItemsCallback)
        {
            Log.Debug($"Try reading cached model: {modelPaths}");
            if (IsCacheOlderThanSave(modelPaths))
            {
                Log.Debug("Cache is older than saved layout data, ignoring cache.");
                return M.NoValue;
            }
            
            string cacheFilePath = GetCacheFilePath(modelPaths);
            return await cacheSerializer.TryDeserializeAsync(cacheFilePath, dataItemsCallback);
        }


        public Task<M<IReadOnlyList<IDataItem>>> TryReadSaveAsync(ModelPaths modelPaths)
        {
            Log.Debug($"Try reading saved model layout: {modelPaths}");
            string saveFilePath = GetSaveFilePath(modelPaths);
            return saveSerializer.DeserializeAsync(saveFilePath);
        }


        public async Task SaveAsync(ModelPaths modelPaths, IReadOnlyList<IDataItem> items)
        {
            Log.Debug($"Saving model layout: {modelPaths}");
            Timing t = Timing.Start();
            await SaveItemsAsync(modelPaths, items);
            t.Log("Save items");

            Log.Debug($"Saving cache layout: {modelPaths}");
            await CacheItemsAsync(modelPaths, items);
            t.Log($"Cache {items.Count} items");
        }


        private async Task CacheItemsAsync(ModelPaths modelPaths, IReadOnlyList<IDataItem> items)
        {
            string cacheFilePath = GetCacheFilePath(modelPaths);

            IReadOnlyList<IDataItem> cacheItems = await GetCacheItemsAsync(items);
            await cacheSerializer.SerializeAsync(cacheItems, cacheFilePath);
        }


        private async Task SaveItemsAsync(ModelPaths modelPaths, IReadOnlyList<IDataItem> items)
        {
            Timing t = Timing.Start();
            IReadOnlyList<IDataItem> saveItems = await GetSaveItemsAsync(items);
            t.Log("Got items");

            string saveFilePath = GetSaveFilePath(modelPaths);

            if (IsSaveNewerThanData(modelPaths))
            {
                await saveSerializer.SerializeMergedAsync(saveItems, saveFilePath);
                t.Log("saved merged");
            }
            else
            {
                await saveSerializer.SerializeAsync(saveItems, saveFilePath);
                t.Log("saved full");
            }
        }


        private static Task<List<IDataItem>> GetSaveItemsAsync(IReadOnlyList<IDataItem> items)
        {
            return Task.Run(() =>
            {
                List<IDataItem> saveItems = new List<IDataItem>();
                foreach (IDataItem dataItem in items)
                {
                    switch (dataItem)
                    {
                        case DataNode dataNode:
                            saveItems.Add(dataNode);
                            break;
                        case DataLine dataLine:
                            if (dataLine.Points.Count > 0) saveItems.Add(dataLine);
                            break;
                    }
                }

                return saveItems;
            });
        }


        private static Task<List<IDataItem>> GetCacheItemsAsync(IReadOnlyList<IDataItem> items) =>
            Task.Run(() => items
                .Where(item => item is DataNode || item is DataLine)
                .Concat(items.Where(item => item is DataLink))
                .ToList());


        private bool IsSaveNewerThanData(ModelPaths modelPaths)
        {
            DateTime saveTime = GetSaveTime(modelPaths);
            DateTime dataTime = parserService.GetDataTime(modelPaths);
            Log.Debug($"Save time: {saveTime}, Data time: {dataTime}");

            if (saveTime == DateTime.MinValue || dataTime == DateTime.MinValue)
            {
                return false;
            }

            return saveTime > dataTime;
        }


        private bool IsCacheOlderThanSave(ModelPaths modelPaths)
        {
            DateTime saveTime = GetSaveTime(modelPaths);
            DateTime cacheTime = GetCacheTime(modelPaths);
            Log.Debug($"Save time: {saveTime}, CacheTime time: {cacheTime}");

            if (saveTime == DateTime.MinValue || cacheTime == DateTime.MinValue)
            {
                return false;
            }

            return cacheTime < saveTime;
        }


        private static string GetCacheFilePath(ModelPaths modelPaths)
        {
            var dataFileName = Path.GetFileNameWithoutExtension(modelPaths.ModelPath);
            string cacheFileName = $"{dataFileName}.cache.json";
            return Path.Combine(modelPaths.WorkFolderPath, cacheFileName);
        }


        private static string GetSaveFilePath(ModelPaths modelPaths)
        {
            string dataFileName = $"{Path.GetFileNameWithoutExtension(modelPaths.ModelPath)}.dpnr";
            string folderPath = Path.GetDirectoryName(modelPaths.ModelPath);
            return Path.Combine(folderPath, dataFileName);
        }
    }
}
