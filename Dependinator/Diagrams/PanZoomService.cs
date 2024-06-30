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
class PanZoomService(
    IScreenService screenService,
    IModelService modelService) : IPanZoomService
{
    const double MaxZoom = 10;
    const double Margin = 10;
    const double WheelZoomSpeed = 1.2;
    const double PinchZoomSpeed = 1.04;

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

        var svgRect = screenService.SvgRect;
        var w = svgRect.Width * newZoom;
        var h = svgRect.Height * newZoom;

        var x = svgX - mx / svgRect.Width * w;
        var y = svgY - my / svgRect.Height * h;

        var newOffset = new Pos(x, y);

        modelService.Do(new ModelEditCommand()
        {
            Offset = newOffset,
            Zoom = newZoom
        }, false);
    }


    public void Pan(PointerEvent e)
    {
        using var model = modelService.UseModel();

        var (dx, dy) = (e.MovementX * model.Zoom, e.MovementY * model.Zoom);
        var newOffset = new Pos(model.Offset.X - dx, model.Offset.Y - dy);

        modelService.Do(new ModelEditCommand() { Offset = newOffset, }, false);
    }


    public void PanZoomOrg(Rect viewRect, double zoom)
    {
        var newOffset = new Pos(viewRect.X, viewRect.Y);
        var newZoom = zoom;

        // Apply the newZoom and newOffset to the viewRect
        using var model = modelService.UseModel();
        model.Offset = newOffset;
        model.Zoom = newZoom;
        Log.Info($"PanZoom newOffset={newOffset} newZoom={newZoom}");
    }


    public void PanZoom(Rect viewRect, double zoom)
    {
        var svgRect = screenService.SvgRect;
        Log.Info($"PanZoom viewRect={viewRect} svgRect={svgRect} zoom={zoom}");

        // Calculate the ratio of the width and height of the svgRect and viewRect
        var widthRatio = svgRect.Width / viewRect.Width;
        var heightRatio = svgRect.Height / viewRect.Height;

        // The newZoom should be the smaller of the two ratios, so that the entire viewRect fits within the svgRect
        var newZoom = zoom / Math.Min(widthRatio, heightRatio);

        var newOffset = new Pos(viewRect.X, viewRect.Y);

        // Apply the newZoom and newOffset to the viewRect
        using var model = modelService.UseModel();
        model.Offset = newOffset;
        model.Zoom = newZoom;
        Log.Info($"PanZoom newOffset={newOffset} newZoom={newZoom}");
    }


    public void PanZoomToFit(Rect totalBounds, double maxZoom = 1, bool noCommand = false)
    {
        using var model = modelService.UseModel();

        Rect b = totalBounds;
        var svgRect = screenService.SvgRect;

        // Determine the X or y zoom that best fits the bounds (including margin)
        var zx = (b.Width + 2 * Margin) / svgRect.Width;
        var zy = (b.Height + 2 * Margin) / svgRect.Height;
        var newZoom = Math.Max(maxZoom, Math.Max(zx, zy));

        // Zoom width and height to fit the bounds
        var w = svgRect.Width * newZoom;
        var h = svgRect.Height * newZoom;

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
