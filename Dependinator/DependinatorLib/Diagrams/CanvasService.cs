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
    double LevelZoom { get; }
    double ActualZoom { get; }
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

    public string SvgViewBox => $"{Offset.X / LevelZoom - TileOffset.X} {Offset.Y / LevelZoom - TileOffset.Y} {SvgRect.Width * Zoom / LevelZoom} {SvgRect.Height * Zoom / LevelZoom}";

    //public string SvgViewBox => $"{Offset.X / LevelZoom} {Offset.Y / LevelZoom} {SvgRect.Width * Zoom / LevelZoom} {SvgRect.Height * Zoom / LevelZoom}";


    public async Task InitAsync(IUIComponent component)
    {
        this.component = component;
        await panZoomService.InitAsync(component);
    }


    public void OnClick(MouseEvent e)
    {
        if (modelService.TryGetNode(e.TargetId, out var node))
        {
            Log.Info($"Node clicked: {node}");
        }
        else
        {
            Log.Info($"No node found at {e.OffsetX},{e.OffsetY}");
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

        // if (TileKey == tile.Key && TileContent == tile.Svg) return Content;  // No change (!! but SvgRect handled included yet)


        var tileZoom = tile.Zoom;

        // var (x, y) = (levelSvg.Offset.X, levelSvg.Offset.Y);
        // (double x, double y) = (50, 50);
        //(double vw, double vh) = (100, 100);

        //(double vw, double vh) = (SvgRect.Width / tileZoom, SvgRect.Height / tileZoom);

        (double x, double y) = (-TileKey.TileSize, -TileKey.TileSize);
        (double vw, double vh) = (TileKey.TileSize * 3, TileKey.TileSize * 3);


        var tileViewBox = $"{x} {y} {vw} {vh}";
        var content = $"""<svg width="{vw}" height="{vh}" viewBox="{tileViewBox}" xmlns="http://www.w3.org/2000/svg">{tile.Svg}</svg>""";

        if (content == Content) return Content;  // No change

        Log.Info($"New content {tile.Key} {tile.Offset} {tile.Zoom}");

        Content = content;
        TileKeyText = tile.Key.ToString();
        // TileContent = tile.Svg;
        // TileKey = tile.Key;

        LevelZoom = tileZoom;
        TileViewBox = tileViewBox;
        TileOffset = new Pos(-tile.Offset.X + x, -tile.Offset.Y + y);
        panZoomService.SvgZoom = tileZoom;

        component?.TriggerStateHasChangedAsync();

        return Content;
    }
}