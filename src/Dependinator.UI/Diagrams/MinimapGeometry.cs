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
