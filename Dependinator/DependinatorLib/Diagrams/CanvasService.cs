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
    int ZCount { get; }

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
    public int Level { get; private set; }
    public Rect SvgRect => panZoomService.SvgRect;
    public Pos Offset => panZoomService.Offset;
    public double Zoom => panZoomService.Zoom;
    public int ZCount => panZoomService.ZCount;

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
        var (svg, zoom, level) = svgContentData.Get(panZoomService.Zoom);

        // Rezise the svg to fit the zoom it was created for
        var (sw, sh) = (SvgRect.Width, SvgRect.Height);
        var vw = sw / zoom;
        var vh = sh / zoom;

        var content = $"""<svg width="{sw}" height="{sh}" viewBox="0 0 {vw} {vh}" xmlns="http://www.w3.org/2000/svg">{svg}</svg>""";
        Level = level;
        return content;

    }

    void SetSvgContent(Svgs svgs)
    {
        svgContentData = svgs;
    }
}