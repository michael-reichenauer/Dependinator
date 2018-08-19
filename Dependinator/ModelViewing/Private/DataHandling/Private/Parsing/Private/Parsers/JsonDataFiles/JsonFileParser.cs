using System;
using System.IO;
using System.Threading.Tasks;
using Dependinator.ModelViewing.Private.DataHandling.Private.Parsing.Private.Parsers.Common;
using Dependinator.Utils;
using Newtonsoft.Json;



namespace Dependinator.ModelViewing.Private.DataHandling.Private.Parsing.Private.Parsers.JsonDataFiles
{
    internal class JsonFileParser : IParser
    {
        private static readonly JsonSerializer Serializer = new JsonSerializer
        {
            Formatting = Formatting.Indented,
            ObjectCreationHandling = ObjectCreationHandling.Replace,
            NullValueHandling = NullValueHandling.Ignore,
            DefaultValueHandling = DefaultValueHandling.Ignore
        };

        private readonly IDataMonitorService dataMonitorService;


        public JsonFileParser(IDataMonitorService dataMonitorService)
        {
            this.dataMonitorService = dataMonitorService;
        }


        public event EventHandler DataChanged
        {
            add => dataMonitorService.DataChangedOccurred += value;
            remove => dataMonitorService.DataChangedOccurred -= value;
        }

        public bool CanSupport(string path) => Path.GetExtension(path).IsSameIc(".json");


        public void StartMonitorDataChanges(string path)
        {
            dataMonitorService.StartMonitorData(path, new[] { path });
        }


        public Task ParseAsync(string path, Action<NodeData> nodeCallback, Action<LinkData> linkCallback)
        {
            return Task.Run(() =>
            {
                try
                {
                    int itemCount;
                    using (FileStream s = File.Open(path, FileMode.Open))
                    using (StreamReader sr = new StreamReader(s))
                    using (JsonReader reader = new JsonTextReader(sr))
                    {
                        ValidateVersion(reader);

                        SkipToItemsStart(reader);

                        itemCount = ReadItems(reader, nodeCallback, linkCallback);
                    }

                    Log.Debug($"Read {itemCount} items");
                }
                catch (Exception e)
                {
                    // Some unexpected error while reading the cache
                    throw new Exception($"Failed to parse:\n{path},\n{e.Message}");
                }
            });
        }


        public Task<NodeDataSource> GetSourceAsync(string path, string nodeName) => 
            Task.FromResult((NodeDataSource)null);


        public Task<string> GetNodeAsync(string path, NodeDataSource source) =>
            Task.FromResult((string)null);


        public DateTime GetDataTime(string path) => File.GetLastWriteTime(path);


        private static void SkipToItemsStart(JsonReader reader)
        {
            // Skip until start of array
            while (reader.Read() && reader.TokenType != JsonToken.StartArray)
            {
            }
        }


        private static int ReadItems(
            JsonReader reader,
            Action<NodeData> nodeCallback,
            Action<LinkData> linkCallback)
        {
            int itemCount = 0;
            while (reader.Read())
            {
                // Deserialize only when there's "{" character in the stream
                if (reader.TokenType == JsonToken.StartObject)
                {
                    JsonTypes.Item jsonItem = Serializer.Deserialize<JsonTypes.Item>(reader);
                    if (jsonItem.Node != null)
                    {
                        nodeCallback(ToNodeData(jsonItem.Node));
                    }
                    else if (jsonItem.Link != null)
                    {
                        linkCallback(ToLinkData(jsonItem.Link));
                    }

                    itemCount++;
                }
            }

            return itemCount;
        }


        private static NodeData ToNodeData(JsonTypes.Node node) =>
            new NodeData(node.Name, node.Parent, node.Type, node.Description);


        private static LinkData ToLinkData(JsonTypes.Link link) =>
            new LinkData(link.Source, link.Target, link.TargetType ?? JsonTypes.NodeType.Type);


        private static void ValidateVersion(JsonReader reader)
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
                        if (versionText != JsonTypes.Version)
                        {
                            throw new FormatException($"Expected {JsonTypes.Version}, was {versionText}");
                        }

                        return;
                    }
                }
            }

            throw new FormatException("Failed to read format version");
        }
    }
}
