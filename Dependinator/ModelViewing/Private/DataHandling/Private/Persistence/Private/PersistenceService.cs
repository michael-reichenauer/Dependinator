using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
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


        public DateTime GetCacheTime(DataFile dataFile)
        {
            string cacheFilePath = GetCacheFilePath(dataFile);
            if (!File.Exists(cacheFilePath))
            {
                return DateTime.MinValue;
            }

            return File.GetLastWriteTime(cacheFilePath);
        }


        public DateTime GetSaveTime(DataFile dataFile)
        {
            string saveFilePath = GetSaveFilePath(dataFile);
            if (!File.Exists(saveFilePath))
            {
                return DateTime.MinValue;
            }

            return File.GetLastWriteTime(saveFilePath);
        }


        public async Task<M> TryReadCacheAsync(DataFile dataFile, DataItemsCallback dataItemsCallback)
        {
            Log.Debug($"Try reading cached model: {dataFile}");
            if (IsCacheOlderThanSave(dataFile))
            {
                Log.Debug("Cache is older than saved layout data, ignoring cache.");
                return M.NoValue;
            }
            
            string cacheFilePath = GetCacheFilePath(dataFile);
            return await cacheSerializer.TryDeserializeAsync(cacheFilePath, dataItemsCallback);
        }


        public Task<M<IReadOnlyList<IDataItem>>> TryReadSaveAsync(DataFile dataFile)
        {
            Log.Debug($"Try reading saved model layout: {dataFile}");
            string saveFilePath = GetSaveFilePath(dataFile);
            return saveSerializer.DeserializeAsync(saveFilePath);
        }


        public async Task SaveAsync(DataFile dataFile, IReadOnlyList<IDataItem> items)
        {
            Log.Debug($"Saving model layout: {dataFile}");
            Timing t = Timing.Start();
            await SaveItemsAsync(dataFile, items);
            t.Log("Save items");

            Log.Debug($"Saving cache layout: {dataFile}");
            await CacheItemsAsync(dataFile, items);
            t.Log($"Cache {items.Count} items");
        }


        private async Task CacheItemsAsync(DataFile dataFile, IReadOnlyList<IDataItem> items)
        {
            string cacheFilePath = GetCacheFilePath(dataFile);

            IReadOnlyList<IDataItem> cacheItems = await GetCacheItemsAsync(items);
            await cacheSerializer.SerializeAsync(cacheItems, cacheFilePath);
        }


        private async Task SaveItemsAsync(DataFile dataFile, IReadOnlyList<IDataItem> items)
        {
            Timing t = Timing.Start();
            IReadOnlyList<IDataItem> saveItems = await GetSaveItemsAsync(items);
            t.Log("Got items");

            string saveFilePath = GetSaveFilePath(dataFile);

            if (IsSaveNewerThanData(dataFile))
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


        private bool IsSaveNewerThanData(DataFile dataFile)
        {
            DateTime saveTime = GetSaveTime(dataFile);
            DateTime dataTime = parserService.GetDataTime(dataFile);
            Log.Debug($"Save time: {saveTime}, Data time: {dataTime}");

            if (saveTime == DateTime.MinValue || dataTime == DateTime.MinValue)
            {
                return false;
            }

            return saveTime > dataTime;
        }


        private bool IsCacheOlderThanSave(DataFile dataFile)
        {
            DateTime saveTime = GetSaveTime(dataFile);
            DateTime cacheTime = GetCacheTime(dataFile);
            Log.Debug($"Save time: {saveTime}, CacheTime time: {cacheTime}");

            if (saveTime == DateTime.MinValue || cacheTime == DateTime.MinValue)
            {
                return false;
            }

            return cacheTime < saveTime;
        }


        private static string GetCacheFilePath(DataFile dataFile)
        {
            var dataFileName = Path.GetFileNameWithoutExtension(dataFile.FilePath);
            string cacheFileName = $"{dataFileName}.cache.json";
            return Path.Combine(dataFile.WorkFolderPath, cacheFileName);
        }


        private static string GetSaveFilePath(DataFile dataFile)
        {
            string dataFileName = $"{Path.GetFileNameWithoutExtension(dataFile.FilePath)}.dpnr";
            string folderPath = Path.GetDirectoryName(dataFile.FilePath);
            return Path.Combine(folderPath, dataFileName);
        }
    }
}
