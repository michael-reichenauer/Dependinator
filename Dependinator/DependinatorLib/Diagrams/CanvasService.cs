using Dependinator.Models;
using Dependinator.Utils.UI;


namespace Dependinator.Diagrams;


interface ICanvasService
{
    Task InitAsync(IUIComponent component);

    string SvgContent { get; }
    int Level { get; }
    Rect SvgRect { get; }
    Pos Offset { get; }
    double Zoom { get; }
    double SvgZoom { get; }
    double ActualZoom { get; }
    int ZCount { get; }
    string ViewBox { get; }

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
    Svgs svgContentData = new(new List<Level>());

    public CanvasService(IMouseEventService mouseEventService, IPanZoomService panZoomService, IModelService modelService)
    {
        this.panZoomService = panZoomService;
        this.modelService = modelService;
        mouseEventService.LeftClick += OnClick;
        mouseEventService.LeftDblClick += OnDblClick;
    }

    public string SvgContent => GetSvgContent();
    public int Level { get; private set; } = -1;
    public double SvgZoom { get; private set; } = 1;
    public string Content { get; private set; } = "";
    public Rect SvgRect => panZoomService.SvgRect;
    public Pos Offset => panZoomService.Offset;
    public double Zoom => panZoomService.Zoom;
    public double ActualZoom => Zoom / SvgZoom;
    public int ZCount => panZoomService.ZCount;

    public string ViewBox => $"{Offset.X / SvgZoom} {Offset.Y / SvgZoom} {SvgRect.Width * Zoom / SvgZoom} {SvgRect.Height * Zoom / SvgZoom}";

    public async Task InitAsync(IUIComponent component)
    {
        this.component = component;
        await panZoomService.InitAsync(component);
    }


    public void OnClick(MouseEvent e)
    {
        Log.Info($"OnClick {e.Type}");
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

        var (content, bounds) = await RefreshAsync();
        SetSvgContent(content);
        //panZoomService.PanZoomToFit(bounds);
        await component.TriggerStateHasChangedAsync();
    }

    public void PanZoomToFit()
    {

    }

    public async void Refresh()
    {
        var (content, bounds) = await RefreshAsync();
        SetSvgContent(content);
        panZoomService.PanZoomToFit(bounds);
        await component.TriggerStateHasChangedAsync();
    }


    public async void Clear()
    {
        modelService.Clear();
        SetSvgContent(new Svgs(new List<Level>()));

        await component.TriggerStateHasChangedAsync();
    }

    public async Task<(Svgs, Rect)> RefreshAsync()
    {
        await modelService.RefreshAsync();
        return modelService.GetSvg();
    }

    string GetSvgContent()
    {
        //Log.Info($"GetSvgContent: Zoom: {panZoomService.Zoom}, Offset: {panZoomService.Offset}, SvgRect: {panZoomService.SvgRect}");
        var (svg, svgZoom, level) = svgContentData.Get(Zoom);
        // Rezise the svg to fit the zoom it was created for
        var (sw, sh) = (SvgRect.Width / svgZoom, SvgRect.Height / svgZoom);
        var vw = sw;
        var vh = sh;

        var content = $"""<svg width="{sw}" height="{sh}" viewBox="0 0 {vw} {vh}" xmlns="http://www.w3.org/2000/svg">{svg}</svg>""";
        //var content = svg;
        if (content == Content) return Content;  // No change
        // Log.Info($"svg: {sw}x{sh} =>  (0 0 {vw} {vh})");
        // Log.Info($"Content: Zoom: {Zoom}=>{svgZoom}, Level: {level}, SvgLength: {svg.Length} sub: (0 0 {vw} {vh})");
        Content = content;
        Level = level;
        SvgZoom = svgZoom;
        panZoomService.SvgZoom = svgZoom;
        component?.TriggerStateHasChangedAsync();

        return Content;

    }

    void SetSvgContent(Svgs svgs)
    {
        svgContentData = svgs;
    }
}