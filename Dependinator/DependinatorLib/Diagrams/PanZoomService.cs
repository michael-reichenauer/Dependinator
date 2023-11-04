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
    void PanZoomToFit(Rect bounds);
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


    // Rect viewRect = new(0, 0, DefaultSize, DefaultSize);
    Pos lastMouse = new(0, 0);
    bool isDrag = false;


    public Rect ViewRect { get; private set; } = Rect.Zero;
    Rect SvgRect { get; set; } = Rect.Zero;

    public double Width => SvgRect.Width;
    public double Height => SvgRect.Height;


    public double Zoom { get; set; } = 1;

    public string ViewRectText => $"{ViewRect.X} {ViewRect.Y} {ViewRect.Width} {ViewRect.Height}";

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

    public void OnMouseWheel(WheelEventArgs e)
    {
        if (e.DeltaY == 0) return;
        var (mx, my) = (e.OffsetX, e.OffsetY);

        double z = 1 - (e.DeltaY > 0 ? -ZoomSpeed : ZoomSpeed);

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

    public void OnMouseMove(MouseEventArgs e)
    {
        var (mx, my) = (e.OffsetX, e.OffsetY);
        //Log.Info($"Mouse: ({mx},{my}) Svg: {svgRect}, View: {viewRect}, Zoom {Zoom}");

        if (e.Buttons == LeftMouseBtn && isDrag)
        {
            var dx = (mx - lastMouse.X) * Zoom;
            var dy = (my - lastMouse.Y) * Zoom;
            lastMouse = new Pos(mx, my);

            ViewRect = ViewRect with { X = ViewRect.X - dx, Y = ViewRect.Y - dy };
        }
    }

    public void OnMouseDown(MouseEventArgs e)
    {
        var (mx, my) = (e.OffsetX, e.OffsetY);
        Log.Info($"Mouse: ({mx},{my}) Svg: {SvgRect}, View: {ViewRect}, Zoom: {Zoom}");
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

    public async Task<Rect> GetSvgRectAsyncZZ()
    {
        var r = await jSInteropService.GetBoundingRectangle(canvas.Ref);
        var windowWidth = Math.Floor(jSInteropService.BrowserSizeDetails.InnerWidth);
        var windowHeight = Math.Floor(jSInteropService.BrowserSizeDetails.InnerHeight);
        var x = r.X;
        var y = r.Y;
        var w = r.Width;
        var margin = 3;//windowWidth - w + x;
        var h = windowHeight - y - margin;

        // Log.Info($"js r: {r.ToJson()}");
        var svgRect = new Rect(0, 0, Math.Floor(w), Math.Floor(h));
        Log.Info($"SvgXX: {svgRect}");
        return svgRect;
    }


    void OnResizing(bool r)
    {
    }

    async void OnResize()
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

                // Re-calculate the view rect for the adjusted SVG, but keep the zoom
                var vw = newSwgRect.Width * Zoom;
                var vh = newSwgRect.Height * Zoom;

                // Adjust view coordinates to fit the new Svg and keep relative position
                var vx = ViewRect.X + (ViewRect.Width - vw) / 2;
                var vy = ViewRect.Y + (ViewRect.Height - vh) / 2;
                var newViewRect = new Rect(vx, vy, vw, vh);

                Log.Info($"Svg {SvgRect} => {newSwgRect}, View: {ViewRect} => {newViewRect}, Zoom: {Zoom}");

                // Adjust SVG and ViewRect to fit the window
                SvgRect = newSwgRect;
                ViewRect = newViewRect;

                canvas.TriggerStateHasChangedAsync().RunInBackground();
            }
        }
    }
}


