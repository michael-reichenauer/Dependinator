using Dependinator.Models;
using Dependinator.Utils.UI;

namespace Dependinator.Diagrams;


interface IPanZoomService
{
    Pos Offset { get; set; }
    double Zoom { get; set; }
    double SvgZoom { get; set; }
    int ZCount { get; }

    void OnMouseWheel(MouseEvent e);
    void OnMouseMove(MouseEvent e);
    void PanZoomToFit(Rect bounds, double maxZoom = 1);
    void PanZoom(Rect viewRect, double zoom);
}


[Scoped]
class PanZoomService : IPanZoomService
{
    const double MaxZoom = 10;
    const double Margin = 10;
    const double WheelZoomSpeed = 1.2;
    const double PinchZoomSpeed = 1.04;

    readonly IScreenService screenService;
    private readonly IApplicationEvents applicationEvents;
    readonly object syncRoot = new();
    private Rect SvgRect => screenService.SvgRect;


    public PanZoomService(IScreenService screenService, IApplicationEvents applicationEvents)
    {
        this.screenService = screenService;
        this.applicationEvents = applicationEvents;
    }


    public int ZCount { get; private set; } = 0;
    public Pos Offset { get; set; } = Pos.None;
    public double Zoom { get; set; } = 1;
    public double SvgZoom { get; set; } = 1;


    public void OnMouseWheel(MouseEvent e)
    {
        lock (syncRoot)
        {
            if (e.DeltaY == 0) return;
            var (mx, my) = (e.OffsetX, e.OffsetY);

            if (e.DeltaY > 0) ZCount--; else ZCount++;

            var speed = e.PointerType == "touch" ? PinchZoomSpeed : WheelZoomSpeed;
            double newZoom = (e.DeltaY > 0) ? Zoom * speed : Zoom * (1 / speed);
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

        applicationEvents.TriggerSaveNeeded();
    }


    public void OnMouseMove(MouseEvent e)
    {
        lock (syncRoot)
        {
            var (dx, dy) = (e.MovementX * Zoom, e.MovementY * Zoom);
            Offset = new Pos(Offset.X - dx, Offset.Y - dy);
        }

        applicationEvents.TriggerSaveNeeded();
    }


    public void PanZoom(Rect viewRect, double zoom)
    {
        lock (syncRoot)
        {
            Offset = new Pos(viewRect.X, viewRect.Y);
            Zoom = zoom;
        }

        applicationEvents.TriggerSaveNeeded();
    }


    public void PanZoomToFit(Rect totalBounds, double maxZoom = 1)
    {
        lock (syncRoot)
        {
            Rect b = totalBounds;
            b = new Rect(b.X, b.Y, b.Width, b.Height);

            // Determine the X or y zoom that best fits the bounds (including margin)
            var zx = (b.Width + 2 * Margin) / SvgRect.Width;
            var zy = (b.Height + 2 * Margin) / SvgRect.Height;
            var newZoom = Math.Max(maxZoom, Math.Max(zx, zy));

            // Zoom width and height to fit the bounds
            var w = SvgRect.Width * newZoom;
            var h = SvgRect.Height * newZoom;

            // Pan to center the bounds
            var x = (b.Width < w) ? b.X - (w - b.Width) / 2 : b.X;
            var y = (b.Height < h) ? b.Y - (h - b.Height) / 2 : b.Y;

            Offset = new Pos(x, y);
            Zoom = newZoom;
        }

        applicationEvents.TriggerSaveNeeded();
    }
}


