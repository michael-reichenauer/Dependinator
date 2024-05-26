using Dependinator.Models;

namespace Dependinator.Diagrams;


interface IPanZoomService
{
    void PanZoomToFit(Rect bounds, double maxZoom = 1, bool noCommand = false);
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

        var newOffset = new Pos(x, y);

        modelService.Do(new ModelEditCommand()
        {
            Offset = newOffset,
            Zoom = newZoom
        });
    }


    public void Pan(PointerEvent e)
    {
        using var model = modelService.UseModel();

        var (dx, dy) = (e.MovementX * model.Zoom, e.MovementY * model.Zoom);
        var newOffset = new Pos(model.Offset.X - dx, model.Offset.Y - dy);

        modelService.Do(new ModelEditCommand() { Offset = newOffset, });
    }


    public void PanZoom(Rect viewRect, double newZoom)
    {
        using var model = modelService.UseModel();

        var newOffset = new Pos(viewRect.X, viewRect.Y);

        model.Offset = newOffset;
        model.Zoom = newZoom;
    }


    public void PanZoomToFit(Rect totalBounds, double maxZoom = 1, bool noCommand = false)
    {
        using var model = modelService.UseModel();

        Rect b = totalBounds;

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

        var newOffset = new Pos(x, y);

        if (noCommand)
        {
            model.Offset = newOffset;
            model.Zoom = newZoom;
            return;
        }

        modelService.Do(new ModelEditCommand()
        {
            Offset = newOffset,
            Zoom = newZoom
        });
    }
}
