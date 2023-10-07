using Microsoft.AspNetCore.Components.Web;

namespace Dependinator.Diagrams;

interface IPanZoomService
{
    string ViewBox { get; }
    double Width { get; }
    double Height { get; }

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
        Zoom = viewBoxRect.W / svgRect.W;
    }

    public void PanZoomToFit(Rect bounds)
    {
        viewBoxRect = new Rect(bounds.X - Margin, bounds.Y - Margin, bounds.W + Margin, bounds.H + Margin);
        ViewBox = $"{viewBoxRect.X} {viewBoxRect.Y} {viewBoxRect.W} {viewBoxRect.H}";
        Zoom = viewBoxRect.W / svgRect.W;
    }

    public void OnMouseWheel(WheelEventArgs e)
    {
        if (e.DeltaY == 0) return;

        double z = 1 - (e.DeltaY > 0 ? -ZoomSpeed : ZoomSpeed);
        double mouseX = e.ClientX - svgRect.X - 5;     // Why 5 and 10 ????
        double mouseY = e.ClientY - svgRect.Y + 10;
        double svgX = mouseX / svgRect.W * this.viewBoxRect.W + this.viewBoxRect.X;
        double svgY = mouseY / svgRect.H * this.viewBoxRect.H + this.viewBoxRect.Y;

        var w = this.viewBoxRect.W * z;
        var h = this.viewBoxRect.H * z;
        var x = svgX - mouseX / svgRect.W * w;
        var y = svgY - mouseY / svgRect.H * h;

        viewBoxRect = new Rect(x, y, w, h);
        ViewBox = $"{viewBoxRect.X} {viewBoxRect.Y} {viewBoxRect.W} {viewBoxRect.H}";
        Zoom = viewBoxRect.W / svgRect.W;
    }

    public void OnMouseMove(MouseEventArgs e)
    {
        if (e.Buttons == LeftMouseBtn && isDrag)
        {
            var dx = (e.OffsetX - lastMouse.X) * Zoom;
            var dy = (e.OffsetY - lastMouse.Y) * Zoom;
            lastMouse = new Pos(e.OffsetX, e.OffsetY);

            viewBoxRect = viewBoxRect with { X = viewBoxRect.X - dx, Y = viewBoxRect.Y - dy };
            ViewBox = $"{viewBoxRect.X} {viewBoxRect.Y} {viewBoxRect.W} {viewBoxRect.H}";
        }
    }

    public void OnMouseDown(MouseEventArgs e)
    {
        if (e.Buttons == LeftMouseBtn)
        {
            isDrag = true;
            lastMouse = new Pos(e.OffsetX, e.OffsetY);
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
        return new Rect(r.Left, r.Top, r.Width, r.Height);
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
            canvas.TriggerStateHasChanged();
        }
    }
}


