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
    //Task<Rect> GetSvgRectAsync();
    void PanZoomToFit(Rect bounds);
}


[Scoped]
class PanZoomService : IPanZoomService
{
    const double Margin = 10;
    const double ZoomSpeed = 0.1;
    const int LeftMouseBtn = 1;
    const int DefaultSize = 400;

    readonly IJSInteropService jSInteropService;
    Canvas canvas = null!;

    // Rect viewRect = new(0, 0, DefaultSize, DefaultSize);
    Pos lastMouse = new(0, 0);
    bool isDrag = false;
    Rect viewRectField = Rect.Zero;
    string viewRectTextField = "";
    Size windowSize = Size.Zero;

    // public Rect SvgRect { get; private set; } = new(0, 0, DefaultSize, DefaultSize);
    public Rect ViewRect
    {
        get => viewRectField;
        private set
        {
            viewRectField = value;
            viewRectTextField = $"{value.X} {value.Y} {value.Width} {value.Height}";
        }
    }

    public double Width => 1000;
    public double Height => 1000;
    Rect SvgRect => new(0, 0, Width, Height);



    public double Zoom { get; set; } = 1;
    // public double Width { get; private set; } = DefaultSize;
    // public double Height { get; private set; } = DefaultSize;
    public string ViewRectText => viewRectTextField;

    public PanZoomService(IJSInteropService jSInteropService)
    {
        this.jSInteropService = jSInteropService;
        jSInteropService.OnResize += OnResize;
        jSInteropService.OnResizing += OnResizing;
        //ViewRect = new(0, 0, DefaultSize, DefaultSize);
    }

    public async Task InitAsync(Canvas canvas)
    {
        this.canvas = canvas;
        await this.jSInteropService.InitializeAsync();

        //SvgRect = await GetSvgRectAsync();
        ViewRect = SvgRect;
        Zoom = ViewRect.Width / SvgRect.Width;
        Log.Info($"Init: Svg: {SvgRect}, View: {ViewRect}, Zoom: {Zoom}");
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
        var w = Math.Floor(jSInteropService.BrowserSizeDetails.InnerWidth);
        var h = Math.Floor(jSInteropService.BrowserSizeDetails.InnerHeight);

        if (canvas != null && (w != windowSize.Width || h != windowSize.Height))
        {
            var newSize = new Size(w, h);
            Log.Info($"Resize ({windowSize}) => ({newSize})");
            windowSize = newSize;

            await canvas.TriggerStateHasChangedAsync();
        }
    }
}


