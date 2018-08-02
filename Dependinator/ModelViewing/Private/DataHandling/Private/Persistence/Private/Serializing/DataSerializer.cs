using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Dependinator.ModelViewing.Private.DataHandling.Dtos;
using Dependinator.Utils;
using Dependinator.Utils.ErrorHandling;
using Dependinator.Utils.Serialization;
using Dependinator.Utils.Threading;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;


namespace Dependinator.ModelViewing.Private.DataHandling.Private.Persistence.Private.Serializing
{
    internal class DataSerializer : IDataSerializer
    {
        public Task SerializeAsync(IReadOnlyList<IDataItem> items, string path)
        {
            return Task.Run(() => SerializeCache(items, path));
        }


        public void SerializeCache(IReadOnlyList<IDataItem> items, string path)
        {
            try
            {
                Timing t = new Timing();
                CacheJsonTypes.Model dataModel = new CacheJsonTypes.Model();

                dataModel.Items = items.Select(Convert.ToCacheJsonItem).ToList();

                t.Log($"Converted {dataModel.Items.Count} data items");

                Json.Serialize(path, dataModel);

                t.Log("Wrote data file");
            }
            catch (Exception e)
            {
                Log.Exception(e, "Failed to serialize");
            }
        }


        public void SerializeSave(IReadOnlyList<IDataItem> items, string path)
        {
            try
            {
                Timing t = new Timing();
                string folder = Path.GetDirectoryName(path);
                string name = Path.GetFileNameWithoutExtension(path);

                int i = 0;
                foreach (IEnumerable<IDataItem> itemBatch in items.Batch(5000000))
                {
                    SaveJsonTypes.Model dataModel = new SaveJsonTypes.Model();

                    dataModel.Items = itemBatch.Select(Convert.ToSaveJsonItem).ToList();

                    string newName = name + $".dn.{i++}.json";
                    path = Path.Combine(folder, newName);

                    Serialize(path, dataModel);
                }

                t.Log("Wrote data file");
            }
            catch (Exception e)
            {
                Log.Exception(e, "Failed to serialize");
            }
        }


        public Task<R> TryDeserializeAsStreamAsync(
            string path, DataItemsCallback dataItemsCallback)
        {
            return Task.Run(() =>
            {
                try
                {
                    Timing t = new Timing();
                    int itemCount = 0;

                    JsonSerializer serializer = Json.Serializer;
                    using (FileStream s = File.Open(path, FileMode.Open))
                    using (StreamReader sr = new StreamReader(s))
                    using (JsonReader reader = new JsonTextReader(sr))
                    {
                        if (!IsValidVersion(reader))
                        {
                            return Error.From(new NotSupportedException("Unexpected format version in data file"));
                        }

                        while (reader.Read())
                        {
                            if (reader.TokenType == JsonToken.StartArray)
                            {
                                break;
                            }
                        }

                        while (reader.Read())
                        {
                            // deserialize only when there's "{" character in the stream
                            if (reader.TokenType == JsonToken.StartObject)
                            {
                                CacheJsonTypes.Item jsonItem = serializer.Deserialize<CacheJsonTypes.Item>(reader);
                                IDataItem modelItem = Convert.ToModelItem(jsonItem);
                                dataItemsCallback(modelItem);
                                itemCount++;
                            }
                        }
                    }

                    t.Log($"Sent all {itemCount} items");
                    return R.Ok;
                }
                catch (Exception e)
                {
                    return new InvalidDataFileException($"Failed to parse:{path},\n{e.Message}");
                }
            });
        }


        private bool IsValidVersion(JsonReader reader)
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
                        return versionText == CacheJsonTypes.Version;
                    }
                }
            }

            return false;
        }


        private static void Serialize(string path, object dataModel)
        {
            JsonSerializer jsonSerializer = CreateSerializer();

            using (StreamWriter stream = new StreamWriter(path))
            {
                jsonSerializer.Serialize(stream, dataModel);
            }
        }


        // Used for serializing data and ignores null and default values
        private static JsonSerializer CreateSerializer()
        {
            JsonSerializer jsonSerializer = new JsonSerializer
            {
                Formatting = Formatting.Indented,
                ObjectCreationHandling = ObjectCreationHandling.Replace,
                NullValueHandling = NullValueHandling.Ignore,
                DefaultValueHandling = DefaultValueHandling.Ignore
            };

            jsonSerializer.Converters.Add(new ItemJsonConverter());

            return jsonSerializer;
        }


        // Converter which serializes items as one line Used when serializing an array of items
        // where each array item is one line in a file.
        private class ItemJsonConverter : JsonConverter
        {
            private static readonly JsonSerializer ItemSerializer = new JsonSerializer
            {
                Formatting = Formatting.None,
                ObjectCreationHandling = ObjectCreationHandling.Replace,
                NullValueHandling = NullValueHandling.Ignore,
                DefaultValueHandling = DefaultValueHandling.Ignore
            };


            public override bool CanRead => false;


            public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
            {
                JToken token = JToken.FromObject(value, ItemSerializer);

                writer.Formatting = Formatting.None;
                writer.WriteWhitespace("\n    ");
                token.WriteTo(writer);
                writer.Formatting = Formatting.Indented;
            }


            public override bool CanConvert(Type objectType) =>
                typeof(SaveJsonTypes.Item).IsAssignableFrom(objectType);


            public override object ReadJson(
                JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer) =>
                throw new NotImplementedException("CanRead is false.");
        }
    }
}
