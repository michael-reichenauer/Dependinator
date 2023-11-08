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
        if (!Try(out var parser, GetParser(path))) return DateTime.MinValue;

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
            using var t = Timing.Start();
            await parser.ParseAsync(path, channel.Writer);
            channel.Writer.Complete();
        }).RunInBackground();

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
        if (parser == null) return R.Error($"No supported parser for {path}");

        return R<IParser>.From(parser);
    }


    class CustomParser : IParser
    {
        public bool CanSupport(string path) => true;

        public async Task<R> ParseAsync(string path, ChannelWriter<IItem> items)
        {
            await items.WriteAsync(new Node("A", "", NodeType.Solution, ""));
            await items.WriteAsync(new Node("B1", "A", NodeType.Assembly, ""));
            await items.WriteAsync(new Node("B2", "A", NodeType.Assembly, ""));

            await items.WriteAsync(new Node("C11", "B1", NodeType.Type, ""));
            await items.WriteAsync(new Node("C12", "B1", NodeType.Type, ""));
            await items.WriteAsync(new Node("C13", "B1", NodeType.Type, ""));

            await items.WriteAsync(new Node("C21", "B2", NodeType.Type, ""));
            await items.WriteAsync(new Node("C22", "B2", NodeType.Type, ""));
            await items.WriteAsync(new Node("C23", "B2", NodeType.Type, ""));

            await items.WriteAsync(new Node("D111", "C11", NodeType.Member, ""));
            await items.WriteAsync(new Node("D112", "C11", NodeType.Type, ""));
            await items.WriteAsync(new Node("D123", "C12", NodeType.Type, ""));

            await items.WriteAsync(new Node("D211", "C21", NodeType.Member, ""));
            await items.WriteAsync(new Node("D212", "C22", NodeType.Type, ""));
            await items.WriteAsync(new Node("D223", "C23", NodeType.Type, ""));

            return R.Ok;
        }

        public Task<R<Source>> GetSourceAsync(string path, string nodeName)
        {
            throw new NotImplementedException();
        }

        public Task<R<string>> GetNodeAsync(string path, Source source)
        {
            throw new NotImplementedException();
        }

        public DateTime GetDataTime(string path)
        {
            throw new NotImplementedException();
        }
    }

    // private static Item ToDataItem(Node node) => new Data(
    //     (DataNodeName)node.Name,
    //     node.Parent != null ? (DataNodeName)node.Parent : null,
    //     ToNodeType(node.Type))
    //     { Description = node.Description };


    // private static IDataItem ToDataItem(LinkData link) => new DataLink(
    //     (DataNodeName)link.Source,
    //     (DataNodeName)link.Target,
    //     ToNodeType(link.TargetType));



    // private static NodeType ToNodeType(string nodeType)
    // {
    //     switch (nodeType)
    //     {
    //         case null:
    //             return NodeType.None;
    //         case NodeData.SolutionType:
    //             return NodeType.Solution;
    //         case NodeData.SolutionFolderType:
    //             return NodeType.SolutionFolder;
    //         case NodeData.AssemblyType:
    //             return NodeType.Assembly;
    //         case NodeData.GroupType:
    //             return NodeType.Group;
    //         case NodeData.DllType:
    //             return NodeType.Dll;
    //         case NodeData.ExeType:
    //             return NodeType.Exe;
    //         case NodeData.NameSpaceType:
    //             return NodeType.NameSpace;
    //         case NodeData.TypeType:
    //             return NodeType.Type;
    //         case NodeData.MemberType:
    //             return NodeType.Member;
    //         default:
    //             throw Asserter.FailFast($"Unexpected type {nodeType}");
    //     }
    // }
}

