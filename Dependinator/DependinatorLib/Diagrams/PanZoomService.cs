using Dependinator.Models;
using Microsoft.AspNetCore.Components.Web;

namespace Dependinator.Diagrams;

interface IPanZoomService
{
    Rect SvgRect { get; }
    Pos Offset { get; }
    double Zoom { get; set; }
    double SvgZoom { get; set; }
    int ZCount { get; }

    Task InitAsync(Canvas canvas);
    void OnMouse(MouseEventArgs e);
    void PanZoomToFit(Rect bounds);
    Task CheckResizeAsync();
}


[Scoped]
class PanZoomService : IPanZoomService
{
    const double MaxZoom = 5;
    const double Margin = 10;
    const double ZoomSpeed = 0.1;
    const int LeftMouseBtn = 1;
    const int SvgPageMargin = 2;

    readonly IJSInteropService jSInteropService;

    readonly object syncRoot = new();
    Canvas canvas = null!;
    Pos lastMouse = Pos.Zero;
    bool isDrag = false;
    public int ZCount { get; private set; } = 0;

    public Pos Offset { get; private set; } = Pos.Zero;
    public Rect SvgRect { get; private set; } = Rect.Zero;

    public double Zoom { get; set; } = 1;
    public double SvgZoom { get; set; } = 1;


    public PanZoomService(IJSInteropService jSInteropService)
    {
        this.jSInteropService = jSInteropService;
        jSInteropService.OnResize += OnResize;
    }

    public async Task InitAsync(Canvas canvas)
    {
        this.canvas = canvas;
        await this.jSInteropService.InitializeAsync();
    }

    public void OnMouse(MouseEventArgs e)
    {
        switch (e.Type)
        {
            case "wheel": OnMouseWheel((WheelEventArgs)e); break;
            case "mousemove": OnMouseMove(e); break;
            case "mousedown": OnMouseDown(e); break;
            case "mouseup": OnMouseUp(e); break;
            default: throw Asserter.FailFast($"Unknown mouse event type: {e.Type}");
        }
    }

    public void PanZoomToFit(Rect totalBounds)
    {
        lock (syncRoot)
        {
            Rect b = totalBounds;
            b = new Rect(b.X, b.Y, b.Width, b.Height);

            // Determine the X or y zoom that best fits the bounds (including margin)
            var zx = (b.Width + 2 * Margin) / SvgRect.Width;
            var zy = (b.Height + 2 * Margin) / SvgRect.Height;
            var newZoom = Math.Max(zx, zy);

            // Zoom width and height to fit the bounds
            var w = SvgRect.Width * newZoom;
            var h = SvgRect.Height * newZoom;

            // Pan to center the bounds
            var x = (b.Width < w) ? b.X - (w - b.Width) / 2 : b.X;
            var y = (b.Height < h) ? b.Y - (h - b.Height) / 2 : b.Y;

            Offset = new Pos(x, y);
            Zoom = newZoom;
        }
    }

    void OnMouseWheel(WheelEventArgs e)
    {
        lock (syncRoot)
        {
            if (e.DeltaY == 0) return;
            var (mx, my) = (e.OffsetX, e.OffsetY);
            // var (mx, my) = (SvgRect.Width / 2, SvgRect.Height / 2);

            double z = 1 - (e.DeltaY > 0 ? -ZoomSpeed : ZoomSpeed);

            var newZoom = Zoom * z;
            if (newZoom > MaxZoom) return;

            if (e.DeltaY > 0) ZCount--; else ZCount++;

            double svgX = mx * Zoom + Offset.X;
            double svgY = my * Zoom + Offset.Y;

            var w = SvgRect.Width * newZoom;
            var h = SvgRect.Height * newZoom;

            var x = svgX - mx / SvgRect.Width * w;
            var y = svgY - my / SvgRect.Height * h;

            Offset = new Pos(x, y);
            Zoom = newZoom;
        }
    }

    void OnMouseMove(MouseEventArgs e)
    {
        var (mx, my) = (e.OffsetX, e.OffsetY);

        if (e.Buttons == LeftMouseBtn && isDrag)
        {
            var dx = (mx - lastMouse.X) * Zoom;
            var dy = (my - lastMouse.Y) * Zoom;
            lastMouse = new Pos(mx, my);

            Offset = new Pos(Offset.X - dx, Offset.Y - dy);
        }
    }

    void OnMouseDown(MouseEventArgs e)
    {
        var (mx, my) = (e.OffsetX, e.OffsetY);
        // Log.Info($"Mouse: ({mx},{my}) Svg: {SvgRect}, View: {ViewRect}, Zoom: {Zoom}");
        if (e.Buttons == LeftMouseBtn)
        {
            isDrag = true;
            lastMouse = new Pos(mx, my);
        }
    }

    void OnMouseUp(MouseEventArgs e)
    {
        if (e.Buttons == LeftMouseBtn)
        {
            isDrag = false;
        }
    }

    void OnResize() => CheckResizeAsync().RunInBackground();



    public async Task CheckResizeAsync()
    {
        // Get Svg position (width and height are unreliable)
        var svg = await jSInteropService.GetBoundingRectangle(canvas.Ref);

        // Get window width and height
        var windowWidth = Math.Floor(jSInteropService.BrowserSizeDetails.InnerWidth);
        var windowHeight = Math.Floor(jSInteropService.BrowserSizeDetails.InnerHeight);

        // Calculate the SVG size to fit the window (with some margin and x,y position)
        var svgX = Math.Floor(svg.X);
        var svgY = Math.Floor(svg.Y);
        var svgWidth = windowWidth - svgX - SvgPageMargin * 2;
        var svgHeight = windowHeight - svgY - SvgPageMargin * 2;
        var newSwgRect = new Rect(0, 0, svgWidth, svgHeight);

        var isChanged = false;
        lock (syncRoot)
        {
            if (newSwgRect != SvgRect)
            {   // Svg size has changed, adjust svg to fit new window size window and trigger update
                SvgRect = newSwgRect;
                isChanged = true;
            }
        }

        if (isChanged)
        {
            canvas.TriggerStateHasChangedAsync().RunInBackground();
            Log.Info($"Resized: {newSwgRect}");
        }
    }
}


