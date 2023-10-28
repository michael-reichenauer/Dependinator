using Dependinator.Models;

namespace Dependinator.Diagrams;


interface ICanvasService
{
    Task InitAsync(Canvas canvas);

    void Refresh();
    void Clear();

    string ZoomTxt { get; }
}


[Scoped]
class CanvasService : ICanvasService
{
    readonly IPanZoomService panZoomService;
    readonly IModelService modelService;
    readonly IModelDb modelDb;

    Canvas canvas = null!;
    Rect bounds = new(0, 0, 0, 0);

    public CanvasService(IPanZoomService panZoomService, IModelService modelService, IModelDb modelDb)
    {
        this.panZoomService = panZoomService;
        this.modelService = modelService;
        this.modelDb = modelDb;
    }

    public string ZoomTxt => $"{panZoomService.Zoom}";

    public Task InitAsync(Canvas canvas)
    {
        this.canvas = canvas;

        return Task.CompletedTask;
    }


    public async void Clear()
    {
        using var _ = Timing.Start();
        using var model = modelDb.GetModel();
        model.Clear();
        canvas.SvgContent = model.Root.ContentSvg;
        await canvas.TriggerStateHasChangedAsync();
    }


    public async void Refresh()
    {
        using var _ = Timing.Start();
        await modelService.RefreshAsync();

        int nodeCount = 0;
        int linkCount = 0;
        int itemCount = 0;
        using (var model = modelDb.GetModel())
        {
            using var __ = Timing.Start("Generate elements");
            canvas.SvgContent = model.Root.ContentSvg;
            bounds = model.Root.TotalRect;
            nodeCount = model.NodeCount;
            linkCount = model.LinkCount;
            itemCount = model.ItemCount;
        }

        Log.Info($"Nodes: {nodeCount}, Links: {linkCount}, Items: {itemCount}");
        Log.Info($"Length: {canvas.SvgContent.Length}");

        panZoomService.PanZoomToFit(bounds);
        await canvas.TriggerStateHasChangedAsync();
    }
}