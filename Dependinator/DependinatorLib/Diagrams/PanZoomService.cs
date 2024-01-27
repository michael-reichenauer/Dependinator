using Dependinator.Models;
using Dependinator.Utils.UI;
using Microsoft.AspNetCore.Components.Web;


namespace Dependinator.Diagrams;

interface IPanZoomService
{
    Rect SvgRect { get; }
    Pos Offset { get; }
    double Zoom { get; set; }
    double SvgZoom { get; set; }
    int ZCount { get; }

    Task InitAsync(IUIComponent component);

    void PanZoomToFit(Rect bounds);
    Task CheckResizeAsync();
}


[Scoped]
class PanZoomService : IPanZoomService
{
    const double MaxZoom = 10;
    const double Margin = 10;
    const double ZoomSpeed = 1.05;
    const int LeftMouseBtn = 1;
    const int SvgPageMargin = 2;

    readonly IJSInteropService jSInteropService;

    readonly object syncRoot = new();


    IUIComponent component = null!;
    public int ZCount { get; private set; } = 0;

    public Pos Offset { get; private set; } = Pos.Zero;
    public Rect SvgRect { get; private set; } = Rect.Zero;

    public double Zoom { get; set; } = 1;
    public double SvgZoom { get; set; } = 1;


    public PanZoomService(IJSInteropService jSInteropService, IMouseEventService mouseEventService)
    {
        this.jSInteropService = jSInteropService;
        jSInteropService.OnResize += OnResize;
        mouseEventService.MouseWheel += OnMouseWheel;
        mouseEventService.MouseMove += OnMouseMove;
    }

    public async Task InitAsync(IUIComponent component)
    {
        await Task.CompletedTask;
        this.component = component;
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

    void OnMouseWheel(MouseEvent e)
    {
        lock (syncRoot)
        {
            if (e.DeltaY == 0) return;
            var (mx, my) = (e.OffsetX, e.OffsetY);

            if (e.DeltaY > 0) ZCount--; else ZCount++;

            double newZoom = (e.DeltaY > 0) ? Zoom * ZoomSpeed : Zoom * (1 / ZoomSpeed);
            if (newZoom > MaxZoom) newZoom = MaxZoom;

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

    void OnMouseMove(MouseEvent e)
    {
        if (e.Buttons == LeftMouseBtn)
        {
            var (dx, dy) = (e.MovementX * Zoom, e.MovementY * Zoom);
            Offset = new Pos(Offset.X - dx, Offset.Y - dy);
        }
    }


    void OnResize() => CheckResizeAsync().RunInBackground();


    public async Task CheckResizeAsync()
    {
        // Get Svg position (width and height are unreliable)
        var svg = await jSInteropService.GetBoundingRectangle(component.Ref);

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
            component.TriggerStateHasChangedAsync().RunInBackground();
            Log.Info($"Resized: {newSwgRect}");
        }
    }
}


