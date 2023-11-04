using Microsoft.AspNetCore.Components.Web;
using Dependinator.Models;

namespace Dependinator.Diagrams;


interface ICanvasService
{
    Task InitAsync(Canvas canvas);

    string SvgContent { get; }
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

    public CanvasService(IPanZoomService panZoomService, IModelService modelService, IModelDb modelDb)
    {
        this.panZoomService = panZoomService;
        this.modelService = modelService;
        this.modelDb = modelDb;
    }

    public string SvgContent { get; private set; } = "";
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

        var (content, bounds) = await RefreshAsync(panZoomService.SvgRect, 1);
        SvgContent = content;
        panZoomService.PanZoomToFit(bounds);
        await canvas.TriggerStateHasChangedAsync();
    }

    public void PanZoomToFit()
    {

    }

    public async void Refresh()
    {
        var (content, bounds) = await RefreshAsync(panZoomService.SvgRect, 1);
        SvgContent = content;
        panZoomService.PanZoomToFit(bounds);
        await canvas.TriggerStateHasChangedAsync();
    }


    public async void Clear()
    {
        using (var model = modelDb.GetModel())
        {
            model.Clear();
            SvgContent = "";
        }

        await canvas.TriggerStateHasChangedAsync();
    }

    public async Task<(string, Rect)> RefreshAsync(Rect viewRect, double zoom)
    {
        await modelService.RefreshAsync();
        using var model = modelDb.GetModel();
        return model.GetSvg(viewRect, zoom);
    }
}