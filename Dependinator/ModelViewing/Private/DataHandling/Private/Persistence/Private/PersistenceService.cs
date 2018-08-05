using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Dependinator.ModelViewing.Private.DataHandling.Dtos;
using Dependinator.ModelViewing.Private.DataHandling.Private.Persistence.Private.Serializing;
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
            string saveFilePath = dataFilePaths.GetSaveFilePath(dataFile);
            string cacheFilePath = dataFilePaths.GetCacheFilePath(dataFile);

            await Task.Run(() =>
            {
                Timing t = Timing.Start();
                IReadOnlyList<IDataItem> saveItems = GetSaveItems(items);

                saveSerializer.Serialize(saveItems, saveFilePath);
                t.Log($"Save {saveItems.Count} items");

                IReadOnlyList<IDataItem> cacheItems = GetCacheItems(items);
                cacheSerializer.Serialize(cacheItems, cacheFilePath);
                t.Log($"Cache {cacheItems.Count} items");
            });
        }


        private static List<IDataItem> GetSaveItems(IReadOnlyList<IDataItem> items)
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
        }


        private static List<IDataItem> GetCacheItems(IReadOnlyList<IDataItem> items) =>
            items
                .Where(item => item is DataNode || item is DataLine)
                .Concat(items.Where(item => item is DataLink)).ToList();
    }
}
