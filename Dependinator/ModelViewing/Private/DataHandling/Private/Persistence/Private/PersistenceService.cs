using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Dependinator.Common.ModelMetadataFolders;
using Dependinator.ModelViewing.Private.DataHandling.Dtos;
using Dependinator.ModelViewing.Private.DataHandling.Private.Persistence.Private.Serializing;
using Dependinator.Utils.ErrorHandling;
using Dependinator.Utils.Threading;


namespace Dependinator.ModelViewing.Private.DataHandling.Private.Persistence.Private
{
    internal class PersistenceService : IPersistenceService
    {
        private readonly ICacheSerializer cacheSerializer;
        private readonly ISaveSerializer saveSerializer;
        private readonly ModelMetadata metadata;


        public PersistenceService(
            ICacheSerializer cacheSerializer,
            ISaveSerializer saveSerializer,
            ModelMetadata metadata)
        {
            this.cacheSerializer = cacheSerializer;
            this.saveSerializer = saveSerializer;
            this.metadata = metadata;
        }


        public async Task<R> TryDeserialize(string dataFilePath, DataItemsCallback dataItemsCallback)
        {
            if (!File.Exists(dataFilePath))
            {
                return Error.From(new MissingDataFileException($"No data file at {dataFilePath}"));
            }

            return await cacheSerializer.TryDeserializeAsStreamAsync(dataFilePath, dataItemsCallback);
        }


        public async Task SaveAsync(IReadOnlyList<IDataItem> items)
        {

            string saveFilePath = GetSaveFilePath();
            string cacheFilePath = GetCacheFilePath();

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


        private string GetCacheFilePath()
        {
            string dataJson = $"{Path.GetFileName(metadata.ModelFilePath)}.dn.json";
            string dataFilePath = Path.Combine(metadata.FolderPath, dataJson);
            return dataFilePath;
        }


        private string GetSaveFilePath()
        {
            string dataJson = $"{Path.GetFileName(metadata.ModelFilePath)}.dpnr";
            string folderPath = Path.GetDirectoryName(metadata.ModelFilePath);
            //  string dataFilePath = Path.Combine(folderPath, dataJson);
            string dataFilePath = Path.Combine(metadata.FolderPath, dataJson);
            return dataFilePath;
        }
    }
}
