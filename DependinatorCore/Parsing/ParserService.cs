using System.Threading.Channels;
using DependinatorCore.Rpc;

namespace DependinatorCore.Parsing;

record ModelPaths(string ModelPath, string WorkFolderPath);

record RemoteResult<T>(T? value, string? error);

[JsonRpc]
internal interface IParserService
{
    //DateTime GetDataTime(string path);

    Task<RemoteResult<IReadOnlyList<Parsing.Item>>> ParseAsync(string path);

    Task<RemoteResult<Source>> GetSourceAsync(string path, string nodeName);

    Task<RemoteResult<string>> TryGetNodeAsync(string path, Source source);
}

[Transient]
class ParserService : IParserService
{
    readonly IEnumerable<IParser> parsers;

    public ParserService(IEnumerable<IParser> parsers)
    {
        this.parsers = parsers;
    }

    // public DateTime GetDataTime(string path)
    // {
    //     if (!Try(out var parser, GetParser(path)))
    //         return DateTime.MinValue;

    //     return parser.GetDataTime(path);
    // }

    public async Task<RemoteResult<IReadOnlyList<Parsing.Item>>> ParseAsync(string path)
    {
        Log.Info($"Parse {path} ...");
        Channel<Item> channel = Channel.CreateUnbounded<Item>();
        IItems items = new ChannelItemsAdapter(channel.Writer);

        if (!Try(out var parser, out var e, GetParser(path)))
            return new RemoteResult<IReadOnlyList<Parsing.Item>>(null, $"File not supported: {path}, {e}");

        await Task.Run(async () =>
        {
            using var t = Timing.Start($"Parsed {path}");
            await parser.ParseAsync(path, items);
            channel.Writer.Complete();
        });

        return new RemoteResult<IReadOnlyList<Parsing.Item>>(await channel.Reader.ReadAllAsync().ToListAsync(), null);
    }

    public async Task<RemoteResult<Source>> GetSourceAsync(string path, string nodeName)
    {
        Log.Debug($"Get source for {nodeName} in model {path}...");

        if (!Try(out var parser, out var e, GetParser(path)))
            return new RemoteResult<Source>(null, $"File not supported: {path}, {e}");

        if (!Try(out var source, out var e2, await parser.GetSourceAsync(path, nodeName)))
            return new RemoteResult<Source>(null, e2.ErrorMessage);

        return new RemoteResult<Source>(source, null);
    }

    public Task<RemoteResult<string>> TryGetNodeAsync(string path, Source source)
    {
        throw new NotImplementedException();
        // Log.Debug($"Get node for {source} in model {modelPaths}...");

        // if (!TryGetParser(modelPaths, out IParser parser))
        // {
        //     return Error.From($"File not supported: {modelPaths}");
        // }

        // NodeDataSource nodeSource = new NodeDataSource(source.Text, source.LineNumber, source.Path);

        // string nodeName = await parser.GetNodeAsync(modelPaths.ModelPath, nodeSource);
        // if (nodeName == null)
        // {
        //     return M.NoValue;
        // }

        // return (DataNodeName)nodeName;
    }

    R<IParser> GetParser(string path)
    {
        // return new CustomParser();

        var parser = parsers.FirstOrDefault(p => p.CanSupport(path));
        if (parser == null)
            return R.Error($"No supported parser for {path}");

        return R<IParser>.From(parser);
    }

    sealed class ChannelItemsAdapter(ChannelWriter<Item> writer) : IItems
    {
        public async Task SendAsync(Node node) => await writer.WriteAsync(new Item(node, null));

        public async Task SendAsync(Link link) => await writer.WriteAsync(new Item(null, link));
    }
}
