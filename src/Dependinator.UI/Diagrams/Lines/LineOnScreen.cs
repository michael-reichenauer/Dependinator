using Dependinator.UI.Modeling.Models;
using Dependinator.UI.Shared.Types;

namespace Dependinator.UI.Diagrams.Lines;

// Where a selected line lies on the screen, for the line toolbar and the line-end tags: the
// line in client pixels, the spot on it for the toolbar, and where it leaves the view on its way
// to an end that is out of view.
static class LineOnScreen
{
    public enum Side
    {
        Left,
        Top,
        Right,
        Bottom,
    }

    // Where a line leaves the view toward one of its ends, and the edge of the view it leaves by.
    public readonly record struct EndTag(Pos At, Side Side);

    // The line's points in client pixels: from its owner's children-local space to the canvas,
    // then through the view (canvas units per pixel, the canvas point at the view's top left) to
    // the page, where the canvas element's top left is svgOrigin.
    public static IReadOnlyList<Pos> ToScreen(Line line, Pos viewOffset, double viewZoom, Pos svgOrigin)
    {
        if (viewZoom <= 0 || !LinePathGeometry.TryGetOwnerNode(line, out var owner))
            return [];

        var (ownerPos, ownerZoom) = owner.GetPosAndZoom();
        var childrenOrigin = new Pos(
            ownerPos.X + owner.ContainerOffset.X * ownerZoom,
            ownerPos.Y + owner.ContainerOffset.Y * ownerZoom
        );
        var childrenZoom = ownerZoom * owner.ContainerZoom;
        return LinePathGeometry
            .GetLocalPolylinePoints(line)
            .Select(p => new Pos(
                svgOrigin.X + (childrenOrigin.X + p.X * childrenZoom - viewOffset.X) / viewZoom,
                svgOrigin.Y + (childrenOrigin.Y + p.Y * childrenZoom - viewOffset.Y) / viewZoom
            ))
            .ToList();
    }

    // The spot for the toolbar: the point that far along the line when it is in view (where the
    // line was clicked), else the middle of the longest stretch of the line in view; null when
    // no part of the line is in view.
    public static Pos? GetAnchor(IReadOnlyList<Pos> points, Rect view, double fraction)
    {
        if (points.Count < 2)
            return null;
        var at = LinePathGeometry.PointAt(points, fraction);
        if (Contains(view, at))
            return at;

        Pos? best = null;
        var bestLength = -1.0;
        for (var i = 0; i < points.Count - 1; i++)
        {
            if (!TryClip(points[i], points[i + 1], view, out var t0, out var t1))
                continue;
            var (a, b) = (points[i], points[i + 1]);
            var length = (t1 - t0) * Distance(a, b);
            if (length <= bestLength)
                continue;
            bestLength = length;
            best = Lerp(a, b, (t0 + t1) / 2);
        }
        return best;
    }

    // Where the line leaves the view toward its source (or target) end, when that end is out of
    // view: the crossing of the view's edge nearest that end along the line. Null when the end is
    // in view, or the line does not cross the view at all.
    public static EndTag? GetEndTag(IReadOnlyList<Pos> points, Rect view, bool isTargetEnd)
    {
        if (points.Count < 2)
            return null;

        // Walk from the end inward: the first segment that reaches into the view enters it at
        // the crossing (its start lies outside, or the walk would have stopped earlier).
        var path = isTargetEnd ? points.Reverse().ToList() : points;
        if (Contains(view, path[0]))
            return null;
        for (var i = 0; i < path.Count - 1; i++)
        {
            if (!TryClip(path[i], path[i + 1], view, out var t0, out _))
                continue;
            var at = Lerp(path[i], path[i + 1], t0);
            return new EndTag(at, SideOf(at, view));
        }
        return null;
    }

    public static bool Contains(Rect view, Pos p) =>
        p.X >= view.X && p.X <= view.X + view.Width && p.Y >= view.Y && p.Y <= view.Y + view.Height;

    // The part of segment a→b inside the view, as fractions t0 ≤ t1 along it (Liang–Barsky).
    static bool TryClip(Pos a, Pos b, Rect view, out double t0, out double t1)
    {
        t0 = 0;
        t1 = 1;
        var dx = b.X - a.X;
        var dy = b.Y - a.Y;
        double[] p = [-dx, dx, -dy, dy];
        double[] q = [a.X - view.X, view.X + view.Width - a.X, a.Y - view.Y, view.Y + view.Height - a.Y];
        for (var i = 0; i < 4; i++)
        {
            if (p[i] == 0)
            {
                if (q[i] < 0)
                    return false; // Parallel to this edge and outside it
                continue;
            }
            var t = q[i] / p[i];
            if (p[i] < 0)
                t0 = Math.Max(t0, t);
            else
                t1 = Math.Min(t1, t);
        }
        return t0 <= t1;
    }

    static Side SideOf(Pos p, Rect view)
    {
        var distances = new (double Distance, Side Side)[]
        {
            (Math.Abs(p.X - view.X), Side.Left),
            (Math.Abs(p.Y - view.Y), Side.Top),
            (Math.Abs(view.X + view.Width - p.X), Side.Right),
            (Math.Abs(view.Y + view.Height - p.Y), Side.Bottom),
        };
        return distances.MinBy(d => d.Distance).Side;
    }

    static Pos Lerp(Pos a, Pos b, double t) => new(a.X + (b.X - a.X) * t, a.Y + (b.Y - a.Y) * t);

    static double Distance(Pos a, Pos b) => Math.Sqrt((b.X - a.X) * (b.X - a.X) + (b.Y - a.Y) * (b.Y - a.Y));
}
