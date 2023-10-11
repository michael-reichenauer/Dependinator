

namespace Dependinator.Model.Parsing;

[Singleton]
internal class ParserService : IParserService
{
    readonly IEnumerable<IParser> parsers;


    public ParserService(IEnumerable<IParser> parsers)
    {
        this.parsers = parsers;
    }


    public DateTime GetDataTime(ModelPaths modelPaths)
    {
        if (!Try(out var parser, out var e, GetParser(modelPaths)))
        {
            return DateTime.MinValue;
        }

        return parser.GetDataTime(modelPaths.ModelPath);
    }


    public async Task<R> ParseAsync(ModelPaths modelPaths, Action<IItems> itemsCallback)
    {
        Log.Debug($"Parse {modelPaths} ...");

        if (!Try(out var parser, out var e, GetParser(modelPaths)))
            return R.Error($"File not supported: {modelPaths}", e);

        await parser.ParseAsync(modelPaths.ModelPath, n => itemsCallback(n), l => itemsCallback(l));
        return R.Ok;
    }


    public async Task<R<Source>> GetSourceAsync(ModelPaths modelPaths, string nodeName)
    {
        Log.Debug($"Get source for {nodeName} in model {modelPaths}...");

        if (!Try(out var parser, out var e, GetParser(modelPaths)))
            return R.Error($"File not supported: {modelPaths}", e);

        return await parser.GetSourceAsync(modelPaths.ModelPath, nodeName);
    }


    public Task<R<string>> TryGetNodeAsync(ModelPaths modelPaths, Source source)
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



    private R<IParser> GetParser(ModelPaths modelPaths)
    {
        var parser = parsers.FirstOrDefault(p => p.CanSupport(modelPaths.ModelPath));
        if (parser == null) return R.Error($"No supported parser for {modelPaths}");

        return (R<IParser>)parser;
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

