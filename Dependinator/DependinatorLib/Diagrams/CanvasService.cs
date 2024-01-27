using Dependinator.Models;
using Dependinator.Utils.UI;


namespace Dependinator.Diagrams;


interface ICanvasService
{
    Task InitAsync(IUIComponent component);

    string SvgContent { get; }
    int LevelNbr { get; }
    Rect SvgRect { get; }
    string LevelViewBox { get; }
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
    public int LevelNbr { get; private set; } = -1;
    public double LevelZoom { get; private set; } = 1;
    public string LevelViewBox { get; private set; } = "";
    public string Content { get; private set; } = "";

    public Rect SvgRect => panZoomService.SvgRect;
    public Pos Offset => panZoomService.Offset;
    public double Zoom => panZoomService.Zoom;
    public double ActualZoom => Zoom / LevelZoom;
    public int ZCount => panZoomService.ZCount;

    public string SvgViewBox => $"{Offset.X / LevelZoom} {Offset.Y / LevelZoom} {SvgRect.Width * Zoom / LevelZoom} {SvgRect.Height * Zoom / LevelZoom}";

    public async Task InitAsync(IUIComponent component)
    {
        this.component = component;
        await panZoomService.InitAsync(component);
    }


    public void OnClick(MouseEvent e)
    {
        Log.Info($"OnClick {e.Type} {e.TargetId}");
        if (modelService.TryGetNode(e.TargetId, out var node))
        {
            Log.Info($"Node clicked: {node}");
        }
        else
        {
            Log.Info($"No node found at {e.OffsetX},{e.OffsetY}");
        }
        // var pos = new Pos(e.OffsetX, e.OffsetY);

        // if (!Try(out var node, modelService.FindNode(Offset, pos, Zoom)))
        // {
        //     Log.Info($"No node found at {pos}");
        //     return;
        // }
        // Log.Info($"Node clicked: {node}");

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

        var levelSvg = modelService.GetSvg(viewRect, Zoom);

        var levelZoom = levelSvg.Zoom;
        var (x, y) = (levelSvg.Offset.X, levelSvg.Offset.Y);
        var (vw, vh) = (SvgRect.Width / levelZoom, SvgRect.Height / levelZoom);

        var levelViewBox = $"{x} {y} {vw} {vh}";

        var content = $"""<svg width="{vw}" height="{vh}" viewBox="{levelViewBox}" xmlns="http://www.w3.org/2000/svg">{levelSvg.Svg}</svg>""";
        //var content = svg;
        if (content == Content) return Content;  // No change
        // Log.Info($"svg: {sw}x{sh} =>  (0 0 {vw} {vh})");
        // Log.Info($"Content: Zoom: {Zoom}=>{svgZoom}, Level: {level}, SvgLength: {svg.Length} sub: (0 0 {vw} {vh})");
        Content = content;
        LevelNbr = levelSvg.Key.Level;

        LevelZoom = levelZoom;
        LevelViewBox = levelViewBox;

        panZoomService.SvgZoom = levelZoom;
        component?.TriggerStateHasChangedAsync();

        return Content;
    }
}