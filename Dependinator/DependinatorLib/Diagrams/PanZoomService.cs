using Dependinator.Models;
using Microsoft.AspNetCore.Components.Web;

namespace Dependinator.Diagrams;

interface IPanZoomService
{
    Rect ViewRect { get; }

    string ViewRectText { get; }
    double Width { get; }
    double Height { get; }
    double Zoom { get; set; }

    Task InitAsync(Canvas canvas);
    void OnMouseWheel(WheelEventArgs e);
    void OnMouseMove(MouseEventArgs e);
    void OnMouseDown(MouseEventArgs e);
    void OnMouseUp(MouseEventArgs e);
    Task<Rect> GetSvgRectAsync();
    void PanZoomToFit(Rect bounds);
}


[Scoped]
class PanZoomService : IPanZoomService
{
    const double Margin = 30;
    const double ZoomSpeed = 0.1;
    const int LeftMouseBtn = 1;
    const int DefaultSize = 400;

    readonly IJSInteropService jSInteropService;
    Canvas canvas = null!;

    Rect svgRect = new(0, 0, DefaultSize, DefaultSize);
    Rect viewRect = new(0, 0, DefaultSize, DefaultSize);
    Pos lastMouse = new(0, 0);
    bool isDrag = false;

    public Rect ViewRect => viewRect;
    public double Zoom { get; set; } = 1;
    public double Width { get; private set; } = DefaultSize;
    public double Height { get; private set; } = DefaultSize;
    public string ViewRectText { get; private set; } = $"0 0 400 400";

    public PanZoomService(IJSInteropService jSInteropService)
    {
        this.jSInteropService = jSInteropService;
        jSInteropService.OnResize += OnResize;
        jSInteropService.OnResizing += OnResizing;
    }

    public async Task InitAsync(Canvas canvas)
    {
        this.canvas = canvas;
        await this.jSInteropService.InitializeAsync();

        svgRect = await GetSvgRectAsync();
        viewRect = svgRect;
        ViewRectText = $"{viewRect.X} {viewRect.Y} {viewRect.Width} {viewRect.Height}";
        Zoom = viewRect.Width / svgRect.Width;
        Log.Info($"Init: Svg: {svgRect}, View: {viewRect}, Zoom: {Zoom}");
    }

    public void PanZoomToFit(Rect bounds)
    {
        viewRect = new Rect(bounds.X - Margin, bounds.Y - Margin, bounds.Width + Margin, bounds.Height + Margin);
        ViewRectText = $"{viewRect.X} {viewRect.Y} {viewRect.Width} {viewRect.Height}";
        Zoom = viewRect.Width / svgRect.Width;
        Log.Info($"Fit: Svg: {svgRect}, View: {viewRect}, Zoom: {Zoom}");
    }

    public void OnMouseWheel(WheelEventArgs e)
    {
        if (e.DeltaY == 0) return;
        var (mx, my) = (e.OffsetX, e.OffsetY);
        //Log.Info($"Mouse: ({mx},{my}) Svg: {svgRect}, View: {viewRect}, Zoom {Zoom}");

        double z = 1 - (e.DeltaY > 0 ? -ZoomSpeed : ZoomSpeed);
        double mouseX = mx - svgRect.X;
        double mouseY = my - svgRect.Y;
        double svgX = mouseX / svgRect.Width * this.viewRect.Width + this.viewRect.X;
        double svgY = mouseY / svgRect.Height * this.viewRect.Height + this.viewRect.Y;

        var w = this.viewRect.Width * z;
        var h = this.viewRect.Height * z;
        var x = svgX - mouseX / svgRect.Width * w;
        var y = svgY - mouseY / svgRect.Height * h;

        viewRect = new Rect(x, y, w, h);
        ViewRectText = $"{viewRect.X} {viewRect.Y} {viewRect.Width} {viewRect.Height}";
        Zoom = viewRect.Width / svgRect.Width;
    }

    public void OnMouseMove(MouseEventArgs e)
    {
        var (mx, my) = (e.OffsetX, e.OffsetY);
        //Log.Info($"Mouse: ({mx},{my}) Svg: {svgRect}, View: {viewRect}, Zoom {Zoom}");

        if (e.Buttons == LeftMouseBtn && isDrag)
        {
            var dx = (mx - lastMouse.X) * Zoom;
            var dy = (my - lastMouse.Y) * Zoom;
            lastMouse = new Pos(mx, my);

            viewRect = viewRect with { X = viewRect.X - dx, Y = viewRect.Y - dy };
            ViewRectText = $"{viewRect.X} {viewRect.Y} {viewRect.Width} {viewRect.Height}";
        }
    }

    public void OnMouseDown(MouseEventArgs e)
    {
        var (mx, my) = (e.OffsetX, e.OffsetY);
        if (e.Buttons == LeftMouseBtn)
        {
            isDrag = true;
            lastMouse = new Pos(mx, my);
        }
    }

    public void OnMouseUp(MouseEventArgs e)
    {
        if (e.Buttons == LeftMouseBtn)
        {
            isDrag = false;
        }
    }

    public async Task<Rect> GetSvgRectAsync()
    {
        var r = await jSInteropService.GetBoundingRectangle(canvas.Ref);
        var windowWidth = jSInteropService.BrowserSizeDetails.InnerWidth;
        var windowHeight = jSInteropService.BrowserSizeDetails.InnerHeight;
        var x = r.X;
        var y = r.Y;
        var w = r.Width;
        var margin = 3;//windowWidth - w + x;
        var h = windowHeight - y - margin;

        Log.Info($"js r: {r.ToJson()}");
        return new Rect(0, 0, w, h);
    }


    void OnResizing(bool r)
    {
    }

    async void OnResize()
    {
        var w = jSInteropService.BrowserSizeDetails.InnerWidth;
        var h = jSInteropService.BrowserSizeDetails.InnerHeight;

        if (canvas != null && (w != Width || h != Height))
        {
            Log.Info($"Resize ({Width},{Height}) => ({w},{h})");
            Width = w;
            Height = h - 60;
            svgRect = await GetSvgRectAsync();
            await canvas.TriggerStateHasChangedAsync();
        }
    }
}


