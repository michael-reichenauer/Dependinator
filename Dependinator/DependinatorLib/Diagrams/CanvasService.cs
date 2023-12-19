using Microsoft.AspNetCore.Components.Web;
using Dependinator.Models;


namespace Dependinator.Diagrams;


interface ICanvasService
{
    Task InitAsync(Canvas canvas);

    string SvgContent { get; }
    int Level { get; }
    Rect SvgRect { get; }
    Pos Offset { get; }
    double Zoom { get; }
    double SvgZoom { get; }
    double ActualZoom { get; }
    int ZCount { get; }
    string ViewBox { get; }

    void OnMouse(MouseEventArgs e);

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
    readonly IModelDb modelDb;

    Canvas canvas = null!;
    Svgs svgContentData = new(new List<Level>());

    public CanvasService(IPanZoomService panZoomService, IModelService modelService, IModelDb modelDb)
    {
        this.panZoomService = panZoomService;
        this.modelService = modelService;
        this.modelDb = modelDb;
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

    public async Task InitAsync(Canvas canvas)
    {
        this.canvas = canvas;
        await panZoomService.InitAsync(canvas);
    }


    public void OnMouse(MouseEventArgs e) => panZoomService.OnMouse(e);


    public async void InitialShow()
    {
        await panZoomService.CheckResizeAsync();

        var (content, bounds) = await RefreshAsync();
        SetSvgContent(content);
        //panZoomService.PanZoomToFit(bounds);
        await canvas.TriggerStateHasChangedAsync();
    }

    public void PanZoomToFit()
    {

    }

    public async void Refresh()
    {
        var (content, bounds) = await RefreshAsync();
        SetSvgContent(content);
        panZoomService.PanZoomToFit(bounds);
        await canvas.TriggerStateHasChangedAsync();
    }


    public async void Clear()
    {
        using (var model = modelDb.GetModel())
        {
            model.Clear();
            SetSvgContent(new Svgs(new List<Level>()));
        }

        await canvas.TriggerStateHasChangedAsync();
    }

    public async Task<(Svgs, Rect)> RefreshAsync()
    {
        await modelService.RefreshAsync();
        using var model = modelDb.GetModel();
        return model.GetSvg();
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
        canvas?.TriggerStateHasChangedAsync();

        return Content;

    }

    void SetSvgContent(Svgs svgs)
    {
        svgContentData = svgs;
    }
}