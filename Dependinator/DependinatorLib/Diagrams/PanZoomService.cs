using Dependinator.Models;
using Microsoft.AspNetCore.Components.Web;

namespace Dependinator.Diagrams;

interface IPanZoomService
{
    Rect ViewRect { get; }

    string ViewBox { get; }
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
    Rect viewBoxRect = new(0, 0, DefaultSize, DefaultSize);
    Pos lastMouse = new(0, 0);
    bool isDrag = false;

    public Rect ViewRect => viewBoxRect;
    public double Zoom { get; set; } = 1;
    public double Width { get; private set; } = DefaultSize;
    public double Height { get; private set; } = DefaultSize;
    public string ViewBox { get; private set; } = $"0 0 400 400";

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
        viewBoxRect = svgRect;
        ViewBox = $"{viewBoxRect.X} {viewBoxRect.Y} {viewBoxRect.Width} {viewBoxRect.Height}";
        Zoom = viewBoxRect.Width / svgRect.Width;
        Log.Info($"Inti: Svg: {svgRect}, View: {viewBoxRect}, Zoom: {Zoom}");
    }

    public void PanZoomToFit(Rect bounds)
    {
        viewBoxRect = new Rect(bounds.X - Margin, bounds.Y - Margin, bounds.Width + Margin, bounds.Height + Margin);
        ViewBox = $"{viewBoxRect.X} {viewBoxRect.Y} {viewBoxRect.Width} {viewBoxRect.Height}";
        Zoom = viewBoxRect.Width / svgRect.Width;
        Log.Info($"Fit: Svg: {svgRect}, View: {viewBoxRect}, Zoom: {Zoom}");
    }

    public void OnMouseWheel(WheelEventArgs e)
    {
        if (e.DeltaY == 0) return;
        var (mx, my) = (e.OffsetX, e.OffsetY);

        double z = 1 - (e.DeltaY > 0 ? -ZoomSpeed : ZoomSpeed);
        double mouseX = mx - svgRect.X;     // Why 5 and 10 ????
        double mouseY = my - svgRect.Y;
        double svgX = mouseX / svgRect.Width * this.viewBoxRect.Width + this.viewBoxRect.X;
        double svgY = mouseY / svgRect.Height * this.viewBoxRect.Height + this.viewBoxRect.Y;

        var w = this.viewBoxRect.Width * z;
        var h = this.viewBoxRect.Height * z;
        var x = svgX - mouseX / svgRect.Width * w;
        var y = svgY - mouseY / svgRect.Height * h;

        viewBoxRect = new Rect(x, y, w, h);
        ViewBox = $"{viewBoxRect.X} {viewBoxRect.Y} {viewBoxRect.Width} {viewBoxRect.Height}";
        Zoom = viewBoxRect.Width / svgRect.Width;
    }

    public void OnMouseMove(MouseEventArgs e)
    {
        var (mx, my) = (e.OffsetX, e.OffsetY);
        // Log.Info($"Mouse: {mx},{my}");

        if (e.Buttons == LeftMouseBtn && isDrag)
        {
            var dx = (mx - lastMouse.X) * Zoom;
            var dy = (my - lastMouse.Y) * Zoom;
            lastMouse = new Pos(mx, my);

            viewBoxRect = viewBoxRect with { X = viewBoxRect.X - dx, Y = viewBoxRect.Y - dy };
            ViewBox = $"{viewBoxRect.X} {viewBoxRect.Y} {viewBoxRect.Width} {viewBoxRect.Height}";
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

        // Not sure why +20 is needed !!!
        return new Rect(0, 0, r.Width, r.Height);
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
            Height = h;
            svgRect = await GetSvgRectAsync();
            await canvas.TriggerStateHasChangedAsync();
        }
    }
}


