using System.Threading.Channels;
using DependinatorCore.Parsing.Sources;
using DependinatorCore.Parsing.Utils;
using DependinatorCore.Rpc;

namespace DependinatorCore.Parsing;

// Parses the a solution or assemblies to extract code structure and meta data.
// Uses both reflection to parse binaries for structure and Roselyn to parse source for more meta data.
[Rpc]
internal interface IParserService
{
    // Parse assemblies
    Task<R<IReadOnlyList<Parsing.Item>>> ParseAsync(string path);

    Task<R<Source>> GetSourceAsync(string path, string nodeName);
}

[Singleton]
class ParserService(IEnumerable<IParser> parsers, ISourceParser sourceParser) : IParserService
{
    readonly IEnumerable<IParser> parsers = parsers;
    private readonly ISourceParser sourceParser = sourceParser;

    public async Task<R<IReadOnlyList<Parsing.Item>>> ParseAsync(string path)
    {
        using var _ = Timing.Start("Parsed sources");
        return await sourceParser.ParseSolutionAsync(path);

        // try
        // {
        //     Channel<Item> channel = Channel.CreateUnbounded<Item>();
        //     var items = new ChannelItemsAdapter(channel.Writer);

        //     if (!Try(out var parser, out var e, GetParser(path)))
        //         return R.Error($"File not supported: {path}", e);

        //     // await Task.Run(async () =>
        //     // {
        //     using var t = Timing.Start($"Parsed {path}");
        //     await parser.ParseAsync(path, items);
        //     channel.Writer.Complete();
        //     // });

        //     var allItems = await channel.Reader.ReadAllAsync().ToListAsync();
        //     Log.Info($"Returning {allItems.Count} items ({items.NodeCount} nodes, {items.LinkCount} links)");
        //     return allItems;
        // }
        // catch (Exception e)
        // {
        //     Log.Exception(e, "Error in parser");
        //     return R.Error("Failed to parse", e);
        // }
    }

    public async Task<R<Source>> GetSourceAsync(string path, string nodeName)
    {
        Log.Debug($"Get source for {nodeName} in model {path}...");

        if (!Try(out var parser, out var e, GetParser(path)))
            return R.Error($"File not supported: {path}", e);

        return await parser.GetSourceAsync(path, nodeName);
    }

    R<IParser> GetParser(string path)
    {
        var parser = parsers.FirstOrDefault(p => p.CanSupport(path));
        if (parser == null)
            return R.Error($"No supported parser for {path}");

        return R<IParser>.From(parser);
    }

    sealed class ChannelItemsAdapter(ChannelWriter<Item> writer) : IItems
    {
        public int NodeCount;
        public int LinkCount;

        public async Task SendAsync(Node node)
        {
            try
            {
                NodeCount++;
                await writer.WriteAsync(new Item(node, null));
            }
            catch (Exception e)
            {
                Log.Exception(e, "Error sending node");
            }
        }

        public async Task SendAsync(Link link)
        {
            try
            {
                LinkCount++;
                await writer.WriteAsync(new Item(null, link));
            }
            catch (Exception e)
            {
                Log.Exception(e, "Error sending link");
            }
        }
    }
}
