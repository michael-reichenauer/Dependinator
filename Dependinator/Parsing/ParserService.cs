using System.Threading.Channels;

namespace Dependinator.Parsing;

record ModelPaths(string ModelPath, string WorkFolderPath);

internal interface IParserService
{
    DateTime GetDataTime(string path);

    R<ChannelReader<IItem>> Parse(string path);

    Task<R<Source>> GetSourceAsync(string path, string nodeName);

    Task<R<string>> TryGetNodeAsync(string path, Source source);
}

[Transient]
class ParserService : IParserService
{
    readonly IEnumerable<IParser> parsers;

    public ParserService(IEnumerable<IParser> parsers)
    {
        this.parsers = parsers;
    }

    public DateTime GetDataTime(string path)
    {
        if (!Try(out var parser, GetParser(path)))
            return DateTime.MinValue;

        return parser.GetDataTime(path);
    }

    public R<ChannelReader<IItem>> Parse(string path)
    {
        Log.Debug($"Parse {path} ...");
        Channel<IItem> channel = Channel.CreateUnbounded<IItem>();

        if (!Try(out var parser, out var e, GetParser(path)))
            return R.Error($"File not supported: {path}", e);

        Task.Run(async () =>
            {
                using var t = Timing.Start($"Parsed {path}");
                await parser.ParseAsync(path, channel.Writer);
                channel.Writer.Complete();
            })
            .RunInBackground();

        return channel.Reader;
    }

    public async Task<R<Source>> GetSourceAsync(string path, string nodeName)
    {
        Log.Debug($"Get source for {nodeName} in model {path}...");

        if (!Try(out var parser, out var e, GetParser(path)))
            return R.Error($"File not supported: {path}", e);

        return await parser.GetSourceAsync(path, nodeName);
    }

    public Task<R<string>> TryGetNodeAsync(string path, Source source)
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
}
