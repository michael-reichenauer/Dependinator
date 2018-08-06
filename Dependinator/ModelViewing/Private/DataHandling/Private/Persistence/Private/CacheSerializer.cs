using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Dependinator.ModelViewing.Private.DataHandling.Dtos;
using Dependinator.ModelViewing.Private.DataHandling.Private.Persistence.Private.JsonTypes;
using Dependinator.Utils;
using Dependinator.Utils.ErrorHandling;
using Dependinator.Utils.Threading;
using Newtonsoft.Json;


namespace Dependinator.ModelViewing.Private.DataHandling.Private.Persistence.Private
{
    internal class CacheSerializer : ICacheSerializer
    {
        private static readonly JsonSerializer Serializer = new JsonSerializer
        {
            Formatting = Formatting.Indented,
            ObjectCreationHandling = ObjectCreationHandling.Replace,
            NullValueHandling = NullValueHandling.Ignore,
            DefaultValueHandling = DefaultValueHandling.Ignore
        };


        public void Serialize(IReadOnlyList<IDataItem> items, string cacheFilePath)
        {
            try
            {
                Timing t = new Timing();
                JsonCacheTypes.Model dataModel = new JsonCacheTypes.Model();

                dataModel.Items = items.Select(Convert.ToCacheJsonItem).ToList();

                t.Log($"Converted {dataModel.Items.Count} data items");

                Serialize(cacheFilePath, dataModel);

                t.Log("Wrote data file");
            }
            catch (Exception e)
            {
                Log.Exception(e, "Failed to serialize");
            }
        }


        public Task<R> TryDeserializeAsync(string cacheFilePath, DataItemsCallback dataItemsCallback)
        {
            return Task.Run(() =>
            {
                if (!File.Exists(cacheFilePath))
                {
                    return R.NoValue;
                }

                try
                {
                    Timing t = new Timing();
                    int itemCount;

                    using (FileStream s = File.Open(cacheFilePath, FileMode.Open))
                    using (StreamReader sr = new StreamReader(s))
                    using (JsonReader reader = new JsonTextReader(sr))
                    {
                        if (!IsValidVersion(reader))
                        {
                            throw new FormatException();
                        }

                        ReadToItemsStart(reader);

                        itemCount = ReadItems(dataItemsCallback, reader);
                    }

                    t.Log($"Sent all {itemCount} items");
                    return R.Ok;
                }
                catch (FormatException)
                {
                    Log.Debug("Unexpected format version in data file");
                }
                catch (Exception e)
                {
                    // Some unexpected error while reading the cache
                    Log.Error($"Failed to parse:{cacheFilePath},\n{e.Message}");
                }

                File.Delete(cacheFilePath);
                return R.NoValue;
            });
        }


        private static void ReadToItemsStart(JsonReader reader)
        {
            while (reader.Read())
            {
                if (reader.TokenType == JsonToken.StartArray)
                {
                    break;
                }
            }
        }


        private static int ReadItems(DataItemsCallback dataItemsCallback, JsonReader reader)
        {
            int itemCount = 0;
            while (reader.Read())
            {
                // deserialize only when there's "{" character in the stream
                if (reader.TokenType == JsonToken.StartObject)
                {
                    JsonCacheTypes.Item jsonItem =
                        Serializer.Deserialize<JsonCacheTypes.Item>(reader);
                    IDataItem modelItem = Convert.ToModelItem(jsonItem);
                    dataItemsCallback(modelItem);
                    itemCount++;
                }
            }

            return itemCount;
        }


        public static void Serialize(string path, object dataModel)
        {
            using (StreamWriter stream = new StreamWriter(path))
            {
                Serializer.Serialize(stream, dataModel);
            }
        }


        private static bool IsValidVersion(JsonReader reader)
        {
            // Look for first property "FormatVersion" and verify that the expected version is found
            if (reader.Read() && reader.TokenType == JsonToken.StartObject)
            {
                if (reader.Read() && reader.TokenType == JsonToken.PropertyName &&
                    (string)reader.Value == "FormatVersion")
                {
                    if (reader.Read() && reader.TokenType == JsonToken.String)
                    {
                        string versionText = (string)reader.Value;
                        if (versionText != JsonCacheTypes.Version)
                        {
                            Log.Warn($"Expected {JsonCacheTypes.Version}, was {versionText}");
                            return false;
                        }

                        return true;
                    }
                }
            }

            Log.Warn("Failed to read format version");
            return false;
        }
    }
}
