using Dependinator.Models;

namespace Dependinator.Diagrams;

interface IPanZoomService
{
    void PanZoomToFit(Rect bounds, double maxZoom = 1, bool noCommand = false);
    void PanZoom(Rect viewRect, double zoom);
    Task PanZoomToAsync(Pos pos, double zoom);
    void Zoom(PointerEvent e);
    void Pan(PointerEvent e);
}

[Scoped]
class PanZoomService(IScreenService screenService, IModelService modelService) : IPanZoomService
{
    const double MaxZoom = 10;
    const double Margin = 10;
    const double WheelZoomSpeed = 1.2;
    const double PinchZoomSpeed = 1.04;
    const double GoToZoomSpeed = 1.02;
    const int GoToMoveStepCount = 50;
    static readonly int GoToDelay = 5;

    public void Zoom(PointerEvent e)
    {
        using var model = modelService.UseModel();

        if (e.DeltaY == 0)
            return;
        var (mx, my) = (e.OffsetX, e.OffsetY);

        var speed = e.PointerType == "touch" ? PinchZoomSpeed : WheelZoomSpeed;
        double newZoom = (e.DeltaY > 0) ? model.Zoom * speed : model.Zoom * (1 / speed);
        if (newZoom > MaxZoom)
            newZoom = MaxZoom;

        double svgX = mx * model.Zoom + model.Offset.X;
        double svgY = my * model.Zoom + model.Offset.Y;

        var svgRect = screenService.SvgRect;
        var w = svgRect.Width * newZoom;
        var h = svgRect.Height * newZoom;

        var x = svgX - mx / svgRect.Width * w;
        var y = svgY - my / svgRect.Height * h;

        var newOffset = new Pos(x, y);

        modelService.Do(new ModelEditCommand() { Offset = newOffset, Zoom = newZoom }, false);
    }

    public void Pan(PointerEvent e)
    {
        using var model = modelService.UseModel();

        var (dx, dy) = (e.MovementX * model.Zoom, e.MovementY * model.Zoom);
        var newOffset = new Pos(model.Offset.X - dx, model.Offset.Y - dy);

        modelService.Do(new ModelEditCommand() { Offset = newOffset }, false);
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

    public async Task PanZoomToAsync(Pos targetPos, double targetZoom)
    {
        var (startPos, startZoom) = GetPosAndZoom();
        var startViewRect = ToViewRect(startPos, startZoom);

        var targetViewRect = ToViewRect(targetPos, targetZoom);

        var currentPos = startPos;
        var currentZoom = startZoom;
        var currentViewRect = ToViewRect(currentPos, currentZoom);

        // Zoom out until the target viewRect fits within the current viewRect
        while (!RectWithin(targetViewRect, currentViewRect))
        {
            currentZoom *= GoToZoomSpeed;
            currentViewRect = ToViewRect(currentPos, currentZoom);

            GoTo(currentPos, currentZoom);
            await Task.Delay(GoToDelay);
        }

        var xd = (targetPos.X - currentPos.X) / currentZoom / GoToMoveStepCount;
        var yd = (targetPos.Y - currentPos.Y) / currentZoom / GoToMoveStepCount;
        for (var i = 0; i < GoToMoveStepCount; i++)
        {
            var x = currentPos.X + xd * currentZoom;
            var y = currentPos.Y + yd * currentZoom;
            currentPos = new Pos(x, y);
            GoTo(currentPos, currentZoom);
            await Task.Delay(GoToDelay);
        }

        GoTo(targetPos, currentZoom);

        // Zoom until pos
        while (currentZoom > targetZoom)
        {
            currentZoom *= 1 / GoToZoomSpeed;
            GoTo(targetPos, currentZoom);
            await Task.Delay(GoToDelay);
        }

        GoTo(targetPos, targetZoom);
    }

    bool RectWithin(Rect r1, Rect r2)
    {
        return r1.X >= r2.X
            && r1.X + r1.Width <= r2.X + r2.Width
            && r1.Y >= r2.Y
            && r1.Y + r1.Height <= r2.Y + r2.Height;
    }

    bool RectOverLaps(Rect r1, Rect r2)
    {
        return r1.X < r2.X + r2.Width && r1.X + r1.Width > r2.X && r1.Y < r2.Y + r2.Height && r1.Y + r1.Height > r2.Y;
    }

    void GoTo(Pos pos, double zoom)
    {
        var offset = ToOffset(pos, zoom);
        modelService.Do(new ModelEditCommand() { Offset = offset, Zoom = zoom }, false);
    }

    (Pos, double) GetPosAndZoom()
    {
        using var model = modelService.UseModel();
        var zoom = model.Zoom;
        var pos = ToPos(model.Offset, zoom);
        return (pos, zoom);
    }

    Rect ToViewRect(Pos pos, double zoom)
    {
        var offset = ToOffset(pos, zoom);
        var svgRect = screenService.SvgRect;
        var x = offset.X;
        var y = offset.Y;
        var width = svgRect.Width * zoom;
        var height = svgRect.Height * zoom;
        return new Rect(x, y, width, height);
    }

    Pos ToPos(Pos offset, double zoom)
    {
        var svgRect = screenService.SvgRect;
        var x = offset.X + svgRect.Width / 2 * zoom;
        var y = offset.Y + svgRect.Height / 2 * zoom;
        return new Pos(x, y);
    }

    Pos ToOffset(Pos pos, double zoom)
    {
        var svgRect = screenService.SvgRect;
        var x = pos.X - svgRect.Width / 2 * zoom;
        var y = pos.Y - svgRect.Height / 2 * zoom;
        return new Pos(x, y);
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

        modelService.Do(new ModelEditCommand() { Offset = newOffset, Zoom = newZoom });
    }
}
