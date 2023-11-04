using Dependinator.Models;
using Microsoft.AspNetCore.Components.Web;

namespace Dependinator.Diagrams;

interface IPanZoomService
{
    Rect SvgRect { get; }
    Rect ViewRect { get; }
    double Zoom { get; set; }
    int ZCount { get; }

    Task InitAsync(Canvas canvas);
    void OnMouse(MouseEventArgs e);
    void PanZoomToFit(Rect bounds);
    Task CheckResizeAsync();
}


[Scoped]
class PanZoomService : IPanZoomService
{
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


    public Rect ViewRect { get; private set; } = Rect.Zero;
    public Pos Offset { get; private set; } = Pos.Zero;
    public Rect SvgRect { get; private set; } = Rect.Zero;

    public double Zoom { get; set; } = 1;


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
        Rect b = totalBounds;
        b = new Rect(b.X, b.Y, b.Width, b.Height);

        // Determine the X or y zoom that best fits the bounds (including margin)
        var zx = (b.Width + 2 * Margin) / SvgRect.Width;
        var zy = (b.Height + 2 * Margin) / SvgRect.Height;
        var z = Math.Max(zx, zy);

        // Zoom width and height to fit the bounds
        var w = ViewRect.Width * z;
        var h = ViewRect.Height * z;

        // Pan to center the bounds
        var x = (b.Width < w) ? b.X - (w - b.Width) / 2 : b.X;
        var y = (b.Height < h) ? b.Y - (h - b.Height) / 2 : b.Y;

        ViewRect = new Rect(x, y, w, h);
        Zoom = ViewRect.Width / SvgRect.Width;
    }

    void OnMouseWheel(WheelEventArgs e)
    {
        if (e.DeltaY == 0) return;
        var (mx, my) = (e.OffsetX, e.OffsetY);

        double z = 1 - (e.DeltaY > 0 ? -ZoomSpeed : ZoomSpeed);
        if (e.DeltaY > 0) ZCount--; else ZCount++;

        double mouseX = mx - SvgRect.X;
        double mouseY = my - SvgRect.Y;

        double svgX = mouseX / SvgRect.Width * ViewRect.Width + ViewRect.X;
        double svgY = mouseY / SvgRect.Height * ViewRect.Height + ViewRect.Y;

        var w = ViewRect.Width * z;
        var h = ViewRect.Height * z;

        var x = svgX - mouseX / SvgRect.Width * w;
        var y = svgY - mouseY / SvgRect.Height * h;

        ViewRect = new Rect(x, y, w, h);
        Zoom = ViewRect.Width / SvgRect.Width;
    }

    void OnMouseMove(MouseEventArgs e)
    {
        var (mx, my) = (e.OffsetX, e.OffsetY);

        if (e.Buttons == LeftMouseBtn && isDrag)
        {
            var dx = (mx - lastMouse.X) * Zoom;
            var dy = (my - lastMouse.Y) * Zoom;
            lastMouse = new Pos(mx, my);

            ViewRect = ViewRect with { X = ViewRect.X - dx, Y = ViewRect.Y - dy };
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

        lock (syncRoot)
        {
            // Get window width and height
            var ww = Math.Floor(jSInteropService.BrowserSizeDetails.InnerWidth);
            var wh = Math.Floor(jSInteropService.BrowserSizeDetails.InnerHeight);

            // Calculate the SVG size to fit the window (with some margin and x,y position)
            var svgWidth = ww - svg.X - SvgPageMargin * 2;
            var svgHeight = wh - svg.Y - SvgPageMargin * 2;

            if (svgWidth != SvgRect.Width || svgHeight != SvgRect.Height)
            {   // Svg size has changed, adjust svg and view to fit new window size window
                var newSwgRect = new Rect(0, 0, svgWidth, svgHeight);

                if (ViewRect == Rect.Zero) ViewRect = newSwgRect;  // Init view first time

                // Match the view rect size for the adjusted Svg size, but keep the zoom level
                var vw = newSwgRect.Width * Zoom;
                var vh = newSwgRect.Height * Zoom;

                // Adjust view coordinates   // to fit the new Svg and keep relative position
                var vx = ViewRect.X;         // ViewRect.X + (ViewRect.Width - vw) / 2;
                var vy = ViewRect.Y;         // ViewRect.Y + (ViewRect.Height - vh) / 2;
                var newViewRect = new Rect(vx, vy, vw, vh);

                // Adjust SVG and ViewRect to fit the window
                SvgRect = newSwgRect;
                ViewRect = newViewRect;

                Log.Info($"Resized: {SvgRect} {ViewRect} {Zoom}");
                canvas.TriggerStateHasChangedAsync().RunInBackground();
            }
        }
    }
}


