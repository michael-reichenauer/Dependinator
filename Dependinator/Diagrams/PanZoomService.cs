using Dependinator.Models;
using Dependinator.Utils.UI;

namespace Dependinator.Diagrams;


interface IPanZoomService
{
    void PanZoomToFit(Rect bounds, double maxZoom = 1);
    void PanZoom(Rect viewRect, double zoom);
    void Zoom(PointerEvent e);
    void Pan(PointerEvent e);
}


[Scoped]
class PanZoomService : IPanZoomService
{
    readonly IScreenService screenService;
    readonly IApplicationEvents applicationEvents;
    readonly IModelService modelService;
    private Rect SvgRect => screenService.SvgRect;

    const double MaxZoom = 10;
    const double Margin = 10;
    const double WheelZoomSpeed = 1.2;
    const double PinchZoomSpeed = 1.04;


    public PanZoomService(
        IScreenService screenService,
        IApplicationEvents applicationEvents,
        IModelService modelService)
    {
        this.screenService = screenService;
        this.applicationEvents = applicationEvents;
        this.modelService = modelService;
    }


    public void Zoom(PointerEvent e)
    {
        modelService.Use(m =>
        {
            if (e.DeltaY == 0) return;
            var (mx, my) = (e.OffsetX, e.OffsetY);

            var speed = e.PointerType == "touch" ? PinchZoomSpeed : WheelZoomSpeed;
            double newZoom = (e.DeltaY > 0) ? m.Zoom * speed : m.Zoom * (1 / speed);
            if (newZoom > MaxZoom) newZoom = MaxZoom;

            double svgX = mx * m.Zoom + m.Offset.X;
            double svgY = my * m.Zoom + m.Offset.Y;

            var w = SvgRect.Width * newZoom;
            var h = SvgRect.Height * newZoom;

            var x = svgX - mx / SvgRect.Width * w;
            var y = svgY - my / SvgRect.Height * h;

            m.Offset = new Pos(x, y);
            m.Zoom = newZoom;
        });
    }


    public void Pan(PointerEvent e)
    {
        modelService.Use(m =>
       {
           var (dx, dy) = (e.MovementX * m.Zoom, e.MovementY * m.Zoom);
           m.Offset = new Pos(m.Offset.X - dx, m.Offset.Y - dy);
       });
    }


    public void PanZoom(Rect viewRect, double zoom)
    {
        modelService.Use(m =>
        {
            m.Offset = new Pos(viewRect.X, viewRect.Y);
            m.Zoom = zoom;
        });
    }


    public void PanZoomToFit(Rect totalBounds, double maxZoom = 1)
    {
        modelService.Use(m =>
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

            m.Offset = new Pos(x, y);
            m.Zoom = newZoom;
        });
    }
}
