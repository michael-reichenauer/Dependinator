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


        public Task SerializeAsync(IReadOnlyList<IDataItem> items, string cacheFilePath)
        {
            return Task.Run(() =>
            {
                try
                {
                    Log.Debug($"Cache model to {cacheFilePath}");
                    JsonCacheTypes.Model dataModel = new JsonCacheTypes.Model();

                    dataModel.Items = items.Select(Convert.ToCacheJsonItem).ToList();

                    Serialize(cacheFilePath, dataModel);
                }
                catch (Exception e)
                {
                    Log.Exception(e, "Failed to cache model");
                }
            });
        }


        public Task<M> TryDeserializeAsync(string cacheFilePath, Action<IDataItem> dataItemsCallback)
        {
            return Task.Run(() =>
            {
                if (!File.Exists(cacheFilePath))
                {
                    Log.Debug($"No cache file at {cacheFilePath}");
                    return M.NoValue;
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

                        SkipToItemsStart(reader);

                        itemCount = ReadItems(dataItemsCallback, reader);
                    }

                    t.Log($"Sent all cached {itemCount} items");
                    return M.Ok;
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
                return M.NoValue;
            });
        }


        private static void SkipToItemsStart(JsonReader reader)
        {
            // Skip until start of array
            while (reader.Read() && reader.TokenType != JsonToken.StartArray)
            {
            }
        }


        private static int ReadItems(Action<IDataItem> dataItemsCallback, JsonReader reader)
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


        private static void Serialize(string path, object dataModel)
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
