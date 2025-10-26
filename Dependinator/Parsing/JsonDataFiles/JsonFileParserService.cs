using System.Threading.Channels;
using Dependinator.Parsing;
using Newtonsoft.Json;

namespace Dependinator.Parsing.JsonDataFiles;

[Transient]
class JsonFileParserService : IParser
{
    static readonly JsonSerializer Serializer = new JsonSerializer
    {
        Formatting = Formatting.Indented,
        ObjectCreationHandling = ObjectCreationHandling.Replace,
        NullValueHandling = NullValueHandling.Ignore,
        DefaultValueHandling = DefaultValueHandling.Ignore,
    };

    public bool CanSupport(string path) => Path.GetExtension(path).IsSameIc(".json");

    public Task<R> ParseAsync(string path, ChannelWriter<IItem> items)
    {
        return Task.Run(async () =>
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

                    itemCount = await ReadItemsAsync(reader, items);
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
        Task.FromResult((R<Source>)new Source(path, "", 0));

    public Task<R<string>> GetNodeAsync(string path, Source source) => Task.FromResult((R<string>)"");

    public DateTime GetDataTime(string path) => File.GetLastWriteTime(path);

    static void SkipToItemsStart(JsonReader reader)
    {
        // Skip until start of array
        while (reader.Read() && reader.TokenType != JsonToken.StartArray) { }
    }

    static async Task<int> ReadItemsAsync(JsonReader reader, ChannelWriter<IItem> items)
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
                    await items.WriteAsync(nodeData);
                }
                else if (jsonItem?.Link != null)
                {
                    var linkData = ToLinkData(jsonItem.Link);
                    await items.WriteAsync(linkData);
                }

                itemCount++;
            }
        }

        return itemCount;
    }

    static Node ToNodeData(JsonTypes.Node node) => new(node.Name, ToNodeAttributes(node));

    static NodeAttributes ToNodeAttributes(JsonTypes.Node node) =>
        new()
        {
            Type = node.Attributes?.Type ?? "",
            Description = node.Attributes?.Description ?? "",
            Parent = node.Parent,
        };

    static Link ToLinkData(JsonTypes.Link link) =>
        new(link.Source, link.Target, new() { TargetType = link.Attributes?.TargetType ?? "" });

    static void ValidateVersion(JsonReader reader)
    {
        // Look for first property "FormatVersion" and verify that the expected version is found
        if (reader.Read() && reader.TokenType == JsonToken.StartObject)
        {
            if (reader.Read() && reader.TokenType == JsonToken.PropertyName && (string?)reader.Value == "FormatVersion")
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
