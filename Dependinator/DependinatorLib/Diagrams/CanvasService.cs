using Dependinator.Models;
using Dependinator.Utils.UI;


namespace Dependinator.Diagrams;


interface ICanvasService
{
    Task InitAsync(IUIComponent component);

    string SvgContent { get; }
    string TileKeyText { get; }
    Rect SvgRect { get; }
    string TileViewBox { get; }
    Pos Offset { get; }
    double Zoom { get; }
    int ZCount { get; }
    string SvgViewBox { get; }

    void Refresh();
    void Clear();
    void PanZoomToFit();
    void InitialShow();
}



[Scoped]
class CanvasService : ICanvasService
{
    readonly IPanZoomService panZoomService;
    readonly IModelService modelService;

    IUIComponent component = null!;

    public CanvasService(IMouseEventService mouseEventService, IPanZoomService panZoomService, IModelService modelService)
    {
        this.panZoomService = panZoomService;
        this.modelService = modelService;
        mouseEventService.LeftClick += OnClick;
        mouseEventService.LeftDblClick += OnDblClick;
    }

    public string SvgContent => GetSvgContent();
    public string TileKeyText { get; private set; } = "()";
    public double LevelZoom { get; private set; } = 1;
    public string TileViewBox { get; private set; } = "";
    public Pos TileOffset { get; private set; } = Pos.Zero;
    public string Content { get; private set; } = "";

    public Rect SvgRect => panZoomService.SvgRect;
    public Pos Offset => panZoomService.Offset;
    public double Zoom => panZoomService.Zoom;
    public double ActualZoom => Zoom / LevelZoom;
    public int ZCount => panZoomService.ZCount;

    string selectedNodeId = "";

    public string SvgViewBox => $"{Offset.X / LevelZoom - TileOffset.X} {Offset.Y / LevelZoom - TileOffset.Y} {SvgRect.Width * Zoom / LevelZoom} {SvgRect.Height * Zoom / LevelZoom}";


    public async Task InitAsync(IUIComponent component)
    {
        this.component = component;
        await panZoomService.InitAsync(component);
    }


    public void OnClick(MouseEvent e)
    {
        if (selectedNodeId == e.TargetId) return;

        if (modelService.TryUpdateNode(e.TargetId, node => node.IsSelected = true))
        {
            Log.Info($"Node clicked: {e.TargetId}");
            if (selectedNodeId != "")
            {
                modelService.TryUpdateNode(selectedNodeId, node => node.IsSelected = false);
            }

            selectedNodeId = e.TargetId;
            this.component?.TriggerStateHasChangedAsync();
        }
        else
        {
            Log.Info($"No node found at {e.OffsetX},{e.OffsetY}");
            if (selectedNodeId != "")
            {
                modelService.TryUpdateNode(selectedNodeId, node => node.IsSelected = false);
                selectedNodeId = "";
                this.component?.TriggerStateHasChangedAsync();
            }
        }
    }

    public void OnDblClick(MouseEvent e)
    {
        Log.Info($"OnDoubleClick {e.Type}");
    }


    public async void InitialShow()
    {
        await panZoomService.CheckResizeAsync();

        await RefreshAsync();

        //panZoomService.PanZoomToFit(bounds);
        await component.TriggerStateHasChangedAsync();
    }

    public void PanZoomToFit()
    {

    }

    public async void Refresh()
    {
        await RefreshAsync();
        // panZoomService.PanZoomToFit(bounds);
        await component.TriggerStateHasChangedAsync();
    }


    public async void Clear()
    {
        modelService.Clear();

        await component.TriggerStateHasChangedAsync();
    }

    public async Task RefreshAsync()
    {
        await modelService.RefreshAsync();
    }

    string GetSvgContent()
    {
        //Log.Info($"GetSvgContent: Zoom: {panZoomService.Zoom}, Offset: {panZoomService.Offset}, SvgRect: {panZoomService.SvgRect}");

        var viewRect = new Rect(Offset.X, Offset.Y, SvgRect.Width, SvgRect.Height);
        var tile = modelService.GetTile(viewRect, Zoom);

        if (Content == tile.Svg) return Content;  // No change

        Content = tile.Svg;
        LevelZoom = tile.Zoom;
        var tileViewRect = tile.Key.GetViewRect();
        TileOffset = new Pos(-tile.Offset.X + tileViewRect.X, -tile.Offset.Y + tileViewRect.Y);
        panZoomService.SvgZoom = tile.Zoom;

        TileKeyText = $"{tile.Key}"; // Log info
        TileViewBox = $"{tileViewRect}"; // Log info

        component?.TriggerStateHasChangedAsync();
        return Content;
    }
}