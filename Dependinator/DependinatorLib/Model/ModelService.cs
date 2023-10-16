using Dependinator.Diagrams;


namespace Dependinator.Model;


record Node(string Name, string Parent, string Type, string Description);
record Link(string Source, string Target);
record Source(string Path, string Text, int LineNumber);

interface IModelService
{
    void TriggerRefresh();
}


[Scoped]
class ModelService : IModelService
{
    readonly ICanvasService canvasService;
    readonly Parsing.IParserService parserService;

    Dictionary<string, Parsing.Node> nodes = new Dictionary<string, Parsing.Node>();
    Dictionary<string, Parsing.Link> links = new Dictionary<string, Parsing.Link>();

    public ModelService(ICanvasService canvasService, Parsing.IParserService parserService)
    {
        this.canvasService = canvasService;
        this.parserService = parserService;
    }

    public async void TriggerRefresh()
    {
        using Timing t = Timing.Start();

        var path = "/workspaces/Dependinator/Dependinator/Dependinator.sln";
        if (!Try(out var reader, out var e, parserService.Parse(path)))
        {
            Log.Warn($"Failed to parse file '{path}': {e}");
            return;
        }

        await foreach (var item in reader.ReadAllAsync())
        {
            switch (item)
            {
                case Parsing.Node node: OnNodeEvent(node); break;
                case Parsing.Link link: OnLinkEvent(link); break;
                default: Asserter.FailFast($"Unknown item type: {item}"); break;
            }
        }
        Log.Info($"Parsed: {nodes.Count} nodes, {links.Count} links");
    }

    void OnNodeEvent(Parsing.Node node)
    {
        nodes[node.Name] = node;
    }

    void OnLinkEvent(Parsing.Link link)
    {
        links[link.Source + link.Target] = link;
    }
}