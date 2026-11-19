using System.Globalization;
using Dependinator.UI.Shared.Types;

namespace Dependinator.UI.Diagrams;

// While the view moves (a pan, a wheel or pinch zoom, a go to flight), the canvas keeps the svg
// it shows and moves it with a CSS transform on the <svg> element instead of a new viewBox. A
// viewBox change makes the browser lay out and repaint the whole diagram every frame (WebKit,
// the iPad's engine, took 60 to 150 ms a frame for the demo's denser views), while a transform
// only moves the already painted layer on the GPU (a 16 ms frame). The real view is drawn once
// it rests, or as soon as the transform would show a part the tile does not hold, or would
// scale the painted layer so much that it gets blurry or too small.
static class GestureTransform
{
    // The most the painted layer is scaled before it is drawn again: zooming in blurs it, zooming
    // out shrinks its details and needs the tile's margin around the view (TileKey: 0.6 of the
    // view on each side holds a scale of 1 / 2.2). A wheel burst of up to five ticks
    // (PanZoomService.WheelZoomSpeed) is then drawn once, when it ends.
    public const double MaxScale = 2;

    // Kept clear inside the area the tile holds, a share of the view's size: nodes at the tile's
    // edge are culled by their position, so the outermost strip may lack a few of them.
    const double EdgeMargin = 0.05;

    // The transform that moves what the shown view drew to where the current view would draw
    // it, or null when it cannot: the scale is out of range or the current view reaches outside
    // the canvas area the shown tile holds (canvas units). Views are top-left offsets and zooms
    // (canvas units per pixel); the transform maps the svg's own pixels to screen pixels,
    // around its top-left corner.
    public static (double X, double Y, double Scale)? Get(
        Pos shownOffset,
        double shownZoom,
        Pos offset,
        double zoom,
        Rect svgRect,
        Rect tileArea
    )
    {
        if (shownZoom <= 0 || zoom <= 0 || svgRect.Width <= 0 || svgRect.Height <= 0)
            return null;
        var scale = shownZoom / zoom;
        if (!double.IsFinite(scale) || scale > MaxScale || scale < 1 / MaxScale)
            return null;

        var viewWidth = svgRect.Width * zoom;
        var viewHeight = svgRect.Height * zoom;
        var marginX = viewWidth * EdgeMargin;
        var marginY = viewHeight * EdgeMargin;
        if (
            offset.X - marginX < tileArea.X
            || offset.Y - marginY < tileArea.Y
            || offset.X + viewWidth + marginX > tileArea.X + tileArea.Width
            || offset.Y + viewHeight + marginY > tileArea.Y + tileArea.Height
        )
            return null;

        // A canvas point c is at svg pixel q = (c - shownOffset) / shownZoom and must show at
        // screen pixel p = (c - offset) / zoom, so p = q * scale + (shownOffset - offset) / zoom.
        var x = (shownOffset.X - offset.X) / zoom;
        var y = (shownOffset.Y - offset.Y) / zoom;
        if (!double.IsFinite(x) || !double.IsFinite(y))
            return null;
        return (x, y, scale);
    }

    // The <svg> element's style for a transform (none: drawn as it is). Content past the
    // element's edge shows while it is moved (the frame around it clips), and will-change keeps
    // the painted layer while it moves. Both only while moving: a transformed element paints
    // above the floating toolbars that come before it in the page.
    public static string Css((double X, double Y, double Scale)? transform) =>
        transform is { } t
            ? string.Create(
                CultureInfo.InvariantCulture,
                $"transform-origin:0 0;transform:translate({t.X:0.###}px,{t.Y:0.###}px) scale({t.Scale:0.#####});will-change:transform;overflow:visible;"
            )
            : "";
}
