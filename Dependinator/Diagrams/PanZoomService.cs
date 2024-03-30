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
        this.modelService = modelService;
    }


    public void Zoom(PointerEvent e)
    {
        using var model = modelService.UseModel();

        if (e.DeltaY == 0) return;
        var (mx, my) = (e.OffsetX, e.OffsetY);

        var speed = e.PointerType == "touch" ? PinchZoomSpeed : WheelZoomSpeed;
        double newZoom = (e.DeltaY > 0) ? model.Zoom * speed : model.Zoom * (1 / speed);
        if (newZoom > MaxZoom) newZoom = MaxZoom;

        double svgX = mx * model.Zoom + model.Offset.X;
        double svgY = my * model.Zoom + model.Offset.Y;

        var w = SvgRect.Width * newZoom;
        var h = SvgRect.Height * newZoom;

        var x = svgX - mx / SvgRect.Width * w;
        var y = svgY - my / SvgRect.Height * h;

        model.Offset = new Pos(x, y);
        model.Zoom = newZoom;
    }


    public void Pan(PointerEvent e)
    {
        using var model = modelService.UseModel();

        var (dx, dy) = (e.MovementX * model.Zoom, e.MovementY * model.Zoom);
        model.Offset = new Pos(model.Offset.X - dx, model.Offset.Y - dy);
    }


    public void PanZoom(Rect viewRect, double zoom)
    {
        using var model = modelService.UseModel();

        model.Offset = new Pos(viewRect.X, viewRect.Y);
        model.Zoom = zoom;
    }


    public void PanZoomToFit(Rect totalBounds, double maxZoom = 1)
    {
        using var model = modelService.UseModel();

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

        model.Offset = new Pos(x, y);
        model.Zoom = newZoom;
    }
}
