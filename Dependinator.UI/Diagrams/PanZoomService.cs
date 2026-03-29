using Dependinator.UI.Modeling.Commands;
using Dependinator.UI.Modeling.Models;
using Dependinator.UI.Shared.Types;

namespace Dependinator.UI.Diagrams;

interface IPanZoomService
{
    void PanZoomToFit(Rect bounds, double maxZoom = 1, bool noCommand = false);
    void PanZoom(Rect viewRect, double zoom);
    Task<bool> PanZoomToAsync(Pos pos, double zoom);
    void Zoom(PointerEvent e);
    void Pan(PointerEvent e);
}

[Scoped]
class PanZoomService(
    IScreenService screenService,
    IModelMgr modelMgr,
    ICommandService commandService,
    IApplicationEvents applicationEvents
) : IPanZoomService
{
    const double MaxZoom = 10;
    const double Margin = 10;
    const double WheelZoomSpeed = 1.2;
    const double PinchZoomSpeed = 1.04;
    const double GoToZoomSpeed = 1.02;
    const int GoToMoveStepCount = 50;
    long goToRequestId = 0;

    public void Zoom(PointerEvent e)
    {
        Interlocked.Increment(ref goToRequestId);
        Pos newOffset;
        double newZoom;
        using (var model = modelMgr.UseModel())
        {
            if (e.DeltaY == 0)
                return;
            var (mx, my) = (e.OffsetX, e.OffsetY);

            var speed = e.PointerType == "touch" ? PinchZoomSpeed : WheelZoomSpeed;
            newZoom = (e.DeltaY > 0) ? model.Zoom * speed : model.Zoom * (1 / speed);
            if (newZoom > MaxZoom)
                newZoom = MaxZoom;

            double svgX = mx * model.Zoom + model.Offset.X;
            double svgY = my * model.Zoom + model.Offset.Y;

            var svgRect = screenService.SvgRect;
            var w = svgRect.Width * newZoom;
            var h = svgRect.Height * newZoom;

            var x = svgX - mx / svgRect.Width * w;
            var y = svgY - my / svgRect.Height * h;

            newOffset = new Pos(x, y);
        }

        commandService.Do(new ModelEditCommand() { Offset = newOffset, Zoom = newZoom }, false);
    }

    public void Pan(PointerEvent e)
    {
        Interlocked.Increment(ref goToRequestId);
        Pos newOffset;

        using (var model = modelMgr.UseModel())
        {
            var (dx, dy) = (e.MovementX * model.Zoom, e.MovementY * model.Zoom);
            newOffset = new Pos(model.Offset.X - dx, model.Offset.Y - dy);
        }

        commandService.Do(new ModelEditCommand() { Offset = newOffset }, false);
    }

    public void PanZoomOrg(Rect viewRect, double zoom)
    {
        var newOffset = new Pos(viewRect.X, viewRect.Y);
        var newZoom = zoom;

        // Apply the newZoom and newOffset to the viewRect
        using var model = modelMgr.UseModel();
        model.Offset = newOffset;
        model.Zoom = newZoom;
        Log.Info($"PanZoom newOffset={newOffset} newZoom={newZoom}");
    }

    public async Task<bool> PanZoomToAsync(Pos targetPos, double targetZoom)
    {
        var requestId = Interlocked.Increment(ref goToRequestId);
        if (targetZoom <= 0)
            return false;

        await screenService.CheckResizeAsync();
        if (!IsCurrentGoToRequest(requestId))
            return false;

        var animationRect = screenService.SvgRect;
        if (!IsValidSvgRect(animationRect))
            return false;

        var (startPos, startZoom) = GetPosAndZoom(animationRect);
        Log.Info($"StartPos:  {startPos},  StartZoom:  {startZoom}");
        Log.Info($"TargetPos: {targetPos}, TargetZoom: {targetZoom}");

        var targetViewRect = ToViewRect(targetPos, targetZoom, animationRect);

        var currentPos = startPos;
        var currentZoom = startZoom;
        var currentViewRect = ToViewRect(currentPos, currentZoom, animationRect);

        // Zoom out until the target viewRect fits within the current viewRect
        while (!RectWithin(targetViewRect, currentViewRect))
        {
            if (!IsCurrentGoToRequest(requestId))
                return false;

            currentZoom *= GoToZoomSpeed;
            currentViewRect = ToViewRect(currentPos, currentZoom, animationRect);

            await GoToAsync(currentPos, currentZoom, animationRect);
        }

        var xd = (targetPos.X - currentPos.X) / currentZoom / GoToMoveStepCount;
        var yd = (targetPos.Y - currentPos.Y) / currentZoom / GoToMoveStepCount;
        for (var i = 0; i < GoToMoveStepCount; i++)
        {
            if (!IsCurrentGoToRequest(requestId))
                return false;

            var x = currentPos.X + xd * currentZoom;
            var y = currentPos.Y + yd * currentZoom;
            currentPos = new Pos(x, y);
            await GoToAsync(currentPos, currentZoom, animationRect);
        }

        if (!IsCurrentGoToRequest(requestId))
            return false;
        await GoToAsync(targetPos, currentZoom, animationRect);

        // Zoom until pos
        while (currentZoom > targetZoom)
        {
            if (!IsCurrentGoToRequest(requestId))
                return false;

            currentZoom *= 1 / GoToZoomSpeed;
            await GoToAsync(targetPos, currentZoom, animationRect);
        }

        if (!IsCurrentGoToRequest(requestId))
            return false;

        await screenService.CheckResizeAsync();
        if (!IsCurrentGoToRequest(requestId))
            return false;

        var finalRect = screenService.SvgRect;
        if (!IsValidSvgRect(finalRect))
            return false;
        await GoToAsync(targetPos, targetZoom, finalRect);

        var (endPos, endZoom) = GetPosAndZoom(animationRect);
        Log.Info($"EndPos:  {endPos},  EndZoom:  {endZoom}");
        applicationEvents.TriggerSaveNeeded();
        return true;
    }

    static bool IsValidSvgRect(Rect svgRect) => svgRect.Width > 0 && svgRect.Height > 0;

    bool IsCurrentGoToRequest(long requestId) => requestId == Volatile.Read(ref goToRequestId);

    bool RectWithin(Rect r1, Rect r2)
    {
        return r1.X >= r2.X
            && r1.X + r1.Width <= r2.X + r2.Width
            && r1.Y >= r2.Y
            && r1.Y + r1.Height <= r2.Y + r2.Height;
    }

    async Task GoToAsync(Pos pos, double zoom, Rect svgRect)
    {
        var offset = ToOffset(pos, zoom, svgRect);
        commandService.Do(
            new ModelEditCommand() { Offset = offset, Zoom = zoom },
            isClearCache: false,
            isSaveModel: false
        );

        await Task.Delay(5);
    }

    (Pos, double) GetPosAndZoom(Rect svgRect)
    {
        using var model = modelMgr.UseModel();
        var zoom = model.Zoom;
        var pos = ToPos(model.Offset, zoom, svgRect);
        return (pos, zoom);
    }

    Rect ToViewRect(Pos pos, double zoom)
    {
        return ToViewRect(pos, zoom, screenService.SvgRect);
    }

    Rect ToViewRect(Pos pos, double zoom, Rect svgRect)
    {
        var offset = ToOffset(pos, zoom, svgRect);
        var x = offset.X;
        var y = offset.Y;
        var width = svgRect.Width * zoom;
        var height = svgRect.Height * zoom;
        return new Rect(x, y, width, height);
    }

    Pos ToPos(Pos offset, double zoom)
    {
        return ToPos(offset, zoom, screenService.SvgRect);
    }

    Pos ToPos(Pos offset, double zoom, Rect svgRect)
    {
        var x = offset.X + svgRect.Width / 2 * zoom;
        var y = offset.Y + svgRect.Height / 2 * zoom;
        return new Pos(x, y);
    }

    Pos ToOffset(Pos pos, double zoom)
    {
        return ToOffset(pos, zoom, screenService.SvgRect);
    }

    static Pos ToOffset(Pos pos, double zoom, Rect svgRect)
    {
        var x = pos.X - svgRect.Width / 2 * zoom;
        var y = pos.Y - svgRect.Height / 2 * zoom;
        return new Pos(x, y);
    }

    public void PanZoom(Rect viewRect, double zoom)
    {
        Interlocked.Increment(ref goToRequestId);
        var svgRect = screenService.SvgRect;
        Log.Info($"PanZoom viewRect={viewRect} svgRect={svgRect} zoom={zoom}");

        // Calculate the ratio of the width and height of the svgRect and viewRect
        var widthRatio = svgRect.Width / viewRect.Width;
        var heightRatio = svgRect.Height / viewRect.Height;

        // The newZoom should be the smaller of the two ratios, so that the entire viewRect fits within the svgRect
        var newZoom = zoom / Math.Min(widthRatio, heightRatio);

        var newOffset = new Pos(viewRect.X, viewRect.Y);

        // Apply the newZoom and newOffset to the viewRect
        using var model = modelMgr.UseModel();
        model.Offset = newOffset;
        model.Zoom = newZoom;
        Log.Info($"PanZoom newOffset={newOffset} newZoom={newZoom}");
    }

    public void PanZoomToFit(Rect totalBounds, double maxZoom = 1, bool noCommand = false)
    {
        Interlocked.Increment(ref goToRequestId);
        Pos newOffset;
        double newZoom;
        using (var model = modelMgr.UseModel())
        {
            Rect b = totalBounds;
            var svgRect = screenService.SvgRect;

            // Determine the X or y zoom that best fits the bounds (including margin)
            var zx = (b.Width + 2 * Margin) / svgRect.Width;
            var zy = (b.Height + 2 * Margin) / svgRect.Height;
            newZoom = Math.Max(maxZoom, Math.Max(zx, zy));

            // Zoom width and height to fit the bounds
            var w = svgRect.Width * newZoom;
            var h = svgRect.Height * newZoom;

            // Pan to center the bounds
            var x = (b.Width < w) ? b.X - (w - b.Width) / 2 : b.X;
            var y = (b.Height < h) ? b.Y - (h - b.Height) / 2 : b.Y;

            newOffset = new Pos(x, y);

            if (noCommand)
            {
                model.Offset = newOffset;
                model.Zoom = newZoom;
                return;
            }
        }

        commandService.Do(new ModelEditCommand() { Offset = newOffset, Zoom = newZoom });
    }
}
