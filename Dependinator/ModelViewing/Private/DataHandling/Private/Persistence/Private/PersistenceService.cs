using System.Collections.Generic;
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


        public async Task<R> TryReadCacheAsync(DataFile dataFile, DataItemsCallback dataItemsCallback)
        {
            string cacheFilePath = dataFilePaths.GetCacheFilePath(dataFile);

            return await cacheSerializer.TryDeserializeAsync(cacheFilePath, dataItemsCallback);
        }


        public async Task SaveAsync(DataFile dataFile, IReadOnlyList<IDataItem> items)
        {
            Timing t = Timing.Start();
            await SaveItemsAsync(dataFile, items);
            t.Log("Save items");

            await CacheItemsAsync(dataFile, items);
            t.Log($"Cache {items.Count} items");
        }


        private async Task CacheItemsAsync(DataFile dataFile, IReadOnlyList<IDataItem> items)
        {
            string cacheFilePath = dataFilePaths.GetCacheFilePath(dataFile);

            IReadOnlyList<IDataItem> cacheItems = await GetCacheItemsAsync(items);
            await cacheSerializer.SerializeAsync(cacheItems, cacheFilePath);
        }


        private async Task SaveItemsAsync(DataFile dataFile, IReadOnlyList<IDataItem> items)
        {
            string saveFilePath = dataFilePaths.GetSaveFilePath(dataFile);

            var dataPaths = dataFilePaths.GetDataFilePaths(dataFile);


            IReadOnlyList<IDataItem> saveItems = await GetSaveItemsAsync(items);

            await saveSerializer.SerializeAsync(saveItems, saveFilePath);
        }


        private static Task<List<IDataItem>> GetSaveItemsAsync(IReadOnlyList<IDataItem> items)
        {
            return Task.Run(() =>
            {
                List<IDataItem> saveItems = new List<IDataItem>();
                foreach (IDataItem dataItem in items)
                {
                    if (dataItem is DataNode dataNode)
                    {
                        saveItems.Add(dataNode);
                    }

                    if (dataItem is DataLine dataLine)
                    {
                        if (dataLine.Points.Count > 0)
                        {
                            saveItems.Add(dataLine);
                        }
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
    }
}
