using Dependinator.Diagrams;


namespace Dependinator.Model;


record Node(string Name, string Parent, string Type, string Description);
record Link(string Source, string Target);

interface IModelService
{
    void TriggerRefresh();
}


[Scoped]
class ModelService : IModelService
{
    readonly ICanvasService canvasService;
    readonly Parsing.IParserService parserService;

    Dictionary<string, Node> nodes = new Dictionary<string, Node>();
    Dictionary<string, Link> links = new Dictionary<string, Link>();

    public ModelService(ICanvasService canvasService, Parsing.IParserService parserService)
    {
        this.canvasService = canvasService;
        this.parserService = parserService;
    }

    public async void TriggerRefresh()
    {
        using Timing t = Timing.Start();

        Log.Info($"TriggerRefresh {Threading.CurrentId}");
        await parserService.ParseAsync("/workspaces/Dependinator/Dependinator/Dependinator.sln",
        node =>
        {
            // Log.Info($"Node Thread {Threading.CurrentId}, {node}");
            //Log.Info($"Item: {node}");
        },
        link =>
        {
            //Log.Info($"Link Thread {Threading.CurrentId}, {link}");
            //Log.Info($"Link: {link}");
        });
        Log.Info($"TriggerRefresh {Threading.CurrentId}");
    }
}