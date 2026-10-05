using Dependinator.UI.Shared.Types;

namespace Dependinator.UI.Diagrams;

// How canvas coordinates map into the minimap box: one uniform scale (so shapes keep their
// proportions) chosen to fit the model's bounds, centered in the box.
readonly record struct MinimapFit(double Scale, double OffsetX, double OffsetY)
{
    public Rect ToMinimap(Rect canvasRect) =>
        new(
            OffsetX + canvasRect.X * Scale,
            OffsetY + canvasRect.Y * Scale,
            canvasRect.Width * Scale,
            canvasRect.Height * Scale
        );

    public Pos ToCanvas(double minimapX, double minimapY) =>
        new((minimapX - OffsetX) / Scale, (minimapY - OffsetY) / Scale);
}

static class MinimapGeometry
{
    public const double Width = 200;
    public const double Height = 130;
    public const double Padding = 5;

    // The diagram may be zoomed in at most this many times further than the map. Beyond that
    // the map zooms in with it and shows the part of the model around the view instead of the
    // whole model, so the frame never shrinks below an eighth of the map.
    public const double MaxZoomRatio = 8;

    // The map scrolls only when the frame leaves this inner part of it (fraction per side), so
    // panning moves the frame first and the map follows lazily, like a camera.
    public const double FollowMargin = 0.15;

    // The part of the model the map shows: the whole model while the view is large enough, else
    // a window of the map's shape sized by MaxZoomRatio, centered on the view when it changes
    // size and otherwise kept until the view's frame runs into the margin. Always inside the
    // model's bounds (unless the window is larger than the model in that direction).
    public static Rect Window(Rect? previous, Rect viewport, Rect model)
    {
        var innerWidth = Width - 2 * Padding;
        var innerHeight = Height - 2 * Padding;
        var scale = Math.Max(viewport.Width * MaxZoomRatio / innerWidth, viewport.Height * MaxZoomRatio / innerHeight);
        var width = Math.Min(innerWidth * scale, model.Width);
        var height = Math.Min(innerHeight * scale, model.Height);
        if (width >= model.Width && height >= model.Height)
            return model;

        // Same size as last time (no zoom change): keep the window and follow lazily; otherwise
        // (first time, or the view changed size) center it on the view.
        double x,
            y;
        if (
            previous is { } kept
            && Math.Abs(kept.Width - width) <= width * 0.01
            && Math.Abs(kept.Height - height) <= height * 0.01
        )
        {
            x = Follow(kept.X, width, viewport.X, viewport.Width);
            y = Follow(kept.Y, height, viewport.Y, viewport.Height);
        }
        else
        {
            x = viewport.X + viewport.Width / 2 - width / 2;
            y = viewport.Y + viewport.Height / 2 - height / 2;
        }

        x = width >= model.Width ? model.X : Math.Clamp(x, model.X, model.X + model.Width - width);
        y = height >= model.Height ? model.Y : Math.Clamp(y, model.Y, model.Y + model.Height - height);
        return new Rect(x, y, width, height);
    }

    // One axis of the lazy follow: keep the window unless the frame leaves the inner part.
    static double Follow(double windowStart, double windowSize, double frameStart, double frameSize)
    {
        var margin = windowSize * FollowMargin;
        if (frameSize >= windowSize - 2 * margin)
            return frameStart + frameSize / 2 - windowSize / 2;
        if (frameStart < windowStart + margin)
            return frameStart - margin;
        if (frameStart + frameSize > windowStart + windowSize - margin)
            return frameStart + frameSize + margin - windowSize;
        return windowStart;
    }

    public static MinimapFit Fit(Rect bounds)
    {
        var innerWidth = Width - 2 * Padding;
        var innerHeight = Height - 2 * Padding;
        var scale = Math.Min(innerWidth / Math.Max(bounds.Width, 1), innerHeight / Math.Max(bounds.Height, 1));
        var offsetX = Padding + (innerWidth - bounds.Width * scale) / 2 - bounds.X * scale;
        var offsetY = Padding + (innerHeight - bounds.Height * scale) / 2 - bounds.Y * scale;
        return new MinimapFit(scale, offsetX, offsetY);
    }

    // The part of a rect that lies inside the box, for the viewport frame when the view is
    // partly (or wholly) outside the model.
    public static Rect Clamp(Rect rect)
    {
        var x1 = Math.Clamp(rect.X, 0, Width);
        var y1 = Math.Clamp(rect.Y, 0, Height);
        var x2 = Math.Clamp(rect.X + rect.Width, 0, Width);
        var y2 = Math.Clamp(rect.Y + rect.Height, 0, Height);
        return new Rect(x1, y1, Math.Max(0, x2 - x1), Math.Max(0, y2 - y1));
    }
}
