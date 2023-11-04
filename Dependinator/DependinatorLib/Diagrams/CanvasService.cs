using Microsoft.AspNetCore.Components.Web;
using Dependinator.Models;

namespace Dependinator.Diagrams;


interface ICanvasService
{
    Task InitAsync(Canvas canvas);

    string SvgContent { get; }
    double Zoom { get; }
    Rect SvgRect { get; }
    Rect ViewRect { get; }

    void OnMouse(MouseEventArgs e);

    void Refresh();
    void Clear();
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

    public string SvgContent { get; private set; } = "";
    public Rect SvgRect => panZoomService.SvgRect;
    public Rect ViewRect => panZoomService.ViewRect;
    public double Zoom => panZoomService.Zoom;

    public async Task InitAsync(Canvas canvas)
    {
        this.canvas = canvas;
        await panZoomService.InitAsync(canvas);

        Refresh();
    }

    public void OnMouse(MouseEventArgs e) => panZoomService.OnMouse(e);

    public async void Clear()
    {
        using (var model = modelDb.GetModel())
        {
            model.Clear();
            SvgContent = "";
        }

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
            Log.Info($"Get {panZoomService.ViewRect}, {panZoomService.Zoom}");
            SvgContent = model.GetSvg(panZoomService.ViewRect, 1);
            bounds = model.Root.TotalBoundary;
            nodeCount = model.NodeCount;
            linkCount = model.LinkCount;
            itemCount = model.ItemCount;
        }

        Log.Info($"Nodes: {nodeCount}, Links: {linkCount}, Items: {itemCount}");
        Log.Info($"Length: {SvgContent.Length}");

        panZoomService.PanZoomToFit(bounds);
        await canvas.TriggerStateHasChangedAsync();
    }
}