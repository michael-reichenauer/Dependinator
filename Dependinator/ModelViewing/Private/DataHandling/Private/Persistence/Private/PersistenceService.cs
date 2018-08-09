using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Dependinator.ModelViewing.Private.DataHandling.Dtos;
using Dependinator.Utils.ErrorHandling;
using Dependinator.Utils.Threading;


namespace Dependinator.ModelViewing.Private.DataHandling.Private.Persistence.Private
{
    internal class PersistenceService : IPersistenceService
    {
        private readonly ICacheSerializer cacheSerializer;
        private readonly IDataFilePaths dataFilePaths;
        private readonly ISaveSerializer saveSerializer;


        public PersistenceService(
            IDataFilePaths dataFilePaths,
            ICacheSerializer cacheSerializer,
            ISaveSerializer saveSerializer)
        {
            this.dataFilePaths = dataFilePaths;
            this.cacheSerializer = cacheSerializer;
            this.saveSerializer = saveSerializer;
        }


        public async Task<M> TryReadCacheAsync(DataFile dataFile, DataItemsCallback dataItemsCallback)
        {
            string cacheFilePath = dataFilePaths.GetCacheFilePath(dataFile);

            return await cacheSerializer.TryDeserializeAsync(cacheFilePath, dataItemsCallback);
        }


        public Task<M<IReadOnlyList<IDataItem>>> TryReadSaveAsync(DataFile dataFile)
        {
            string saveFilePath = dataFilePaths.GetSaveFilePath(dataFile);
            return saveSerializer.DeserializeAsync(saveFilePath);
        }


        public async Task SaveAsync(DataFile dataFile, IReadOnlyList<IDataItem> items)
        {
            Timing t = Timing.Start();
            await SaveItemsAsync(dataFile, items);
            t.Log("Save items");

            //await CacheItemsAsync(dataFile, items);
            //t.Log($"Cache {items.Count} items");
        }


        private async Task CacheItemsAsync(DataFile dataFile, IReadOnlyList<IDataItem> items)
        {
            string cacheFilePath = dataFilePaths.GetCacheFilePath(dataFile);

            IReadOnlyList<IDataItem> cacheItems = await GetCacheItemsAsync(items);
            await cacheSerializer.SerializeAsync(cacheItems, cacheFilePath);
        }


        private async Task SaveItemsAsync(DataFile dataFile, IReadOnlyList<IDataItem> items)
        {
            Timing t = Timing.Start();
            IReadOnlyList<IDataItem> saveItems = await GetSaveItemsAsync(items);
            t.Log("Got items");

            string saveFilePath = dataFilePaths.GetSaveFilePath(dataFile);

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

           

            //Timing t = Timing.Start();
            //var it = await saveSerializer.DeserializeAsync(saveFilePath);
            //t.Log("Deserialized data");
            //await saveSerializer.SerializeAsync(it.Value, saveFilePath + ".read");
            //t.Log("Serialized data");
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
            string saveFilePath = dataFilePaths.GetSaveFilePath(dataFile);
            var dataPaths = dataFilePaths.GetDataFilePaths(dataFile);

            if (!File.Exists(saveFilePath))
            {
                return false;
            }


            DateTime saveTime = File.GetLastWriteTime(saveFilePath);
            foreach (string dataFilePath in dataPaths)
            {
                if (!File.Exists(dataFilePath)) return true;

                DateTime fileTime = File.GetLastWriteTime(dataFilePath);
                if (saveTime > fileTime)
                {
                    return true;
                }
            }


            return false;
        }
    }
}
