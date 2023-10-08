﻿using Dependinator.Model.Parsing.Common;
using Newtonsoft.Json;



namespace Dependinator.Model.Parsing.JsonDataFiles;

class JsonFileParser : IParser
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


    public Task<R> ParseAsync(string path, Action<Node> nodeCallback, Action<Link> linkCallback)
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
                return R.Ok;
            }
            catch (Exception e)
            {
                // Some unexpected error while reading the cache
                return R.Error($"Failed to parse:\n{path},\n{e.Message}");
            }
        });
    }


    public Task<R<Source>> GetSourceAsync(string path, string nodeName) =>
        Task.FromResult((R<Source>)new Source("", 0, path));


    public Task<string> GetNodeAsync(string path, Source source) =>
        Task.FromResult("");


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
        Action<Node> nodeCallback,
        Action<Link> linkCallback)
    {
        int itemCount = 0;
        while (reader.Read())
        {
            // Deserialize only when there's "{" character in the stream
            if (reader.TokenType == JsonToken.StartObject)
            {
                JsonTypes.Item? jsonItem = Serializer.Deserialize<JsonTypes.Item>(reader);
                if (jsonItem?.Node != null)
                {
                    var nodeData = ToNodeData(jsonItem.Node);
                    nodeCallback(nodeData);
                }
                else if (jsonItem?.Link != null)
                {
                    var linkData = ToLinkData(jsonItem.Link);
                    linkCallback(linkData);
                }

                itemCount++;
            }
        }

        return itemCount;
    }


    private static Node ToNodeData(JsonTypes.Node node) =>
        new Node(node.Name, node.Parent, node.Type, node.Description);


    private static Link ToLinkData(JsonTypes.Link link) =>
        new Link(link.Source, link.Target, link.TargetType ?? JsonTypes.NodeType.Type);


    private static void ValidateVersion(JsonReader reader)
    {
        // Look for first property "FormatVersion" and verify that the expected version is found
        if (reader.Read() && reader.TokenType == JsonToken.StartObject)
        {
            if (reader.Read() && reader.TokenType == JsonToken.PropertyName &&
                (string?)reader.Value == "FormatVersion")
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

