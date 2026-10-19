using Dependinator.UI.Shared.Types;

namespace Dependinator.UI.Modeling.Layout;

// How readable a computed layout is, measured on its geometry the way the lines are drawn (from
// the source's right edge middle over the waypoints to the target's left edge middle), so the
// fast and the thorough pass are judged alike. Lower scores are better: crossings dominate, a
// line through another node counts more, bends and length break ties.
readonly record struct LayoutQuality(double WeightedCrossings, int LinesThroughNodes, int Bends, double LineLength)
{
    const double LineThroughNodeCost = 4;
    const double BendCost = 0.25;
    const double LengthCostPerUnit = 0.001;

    public double Score =>
        WeightedCrossings + LineThroughNodeCost * LinesThroughNodes + BendCost * Bends + LengthCostPerUnit * LineLength;

    // Spatial buckets keep the pairwise checks local; a cell holds every segment passing it.
    const double CellSize = 400;

    // Node rects shrink by this much before a line counts as passing through one, so lines
    // grazing a corner are not penalized.
    const double NodeMargin = 4;

    public static LayoutQuality Evaluate(LayoutGraph graph, LayoutResult result)
    {
        var segments = new List<Segment>();
        var bends = 0;
        var length = 0.0;
        for (var index = 0; index < graph.Edges.Count; index++)
        {
            var edge = graph.Edges[index];
            if (edge.IsHidden)
                continue;

            var source = result.Boundaries[edge.Source];
            var target = result.Boundaries[edge.Target];
            var waypoints = result.Waypoints[index] ?? [];
            var points = new List<Pos>(waypoints.Count + 2)
            {
                new(source.X + source.Width, source.Y + source.Height / 2),
            };
            points.AddRange(waypoints);
            points.Add(new Pos(target.X, target.Y + target.Height / 2));

            bends += waypoints.Count;
            var weight = 1 + Math.Log2(Math.Max(1, edge.Weight));
            for (var point = 0; point < points.Count - 1; point++)
            {
                segments.Add(new Segment(index, edge.Source, edge.Target, points[point], points[point + 1], weight));
                length += Distance(points[point], points[point + 1]);
            }
        }

        var cells = new Dictionary<(int, int), List<int>>();
        for (var index = 0; index < segments.Count; index++)
        {
            foreach (var cell in Cells(Bounds(segments[index])))
            {
                if (!cells.TryGetValue(cell, out var list))
                    cells[cell] = list = [];
                list.Add(index);
            }
        }

        var crossings = 0.0;
        var counted = new HashSet<(int, int)>();
        foreach (var list in cells.Values)
        {
            for (var first = 0; first < list.Count; first++)
            {
                for (var second = first + 1; second < list.Count; second++)
                {
                    var a = segments[list[first]];
                    var b = segments[list[second]];
                    if (a.Edge == b.Edge || SharesEndpoint(a, b))
                        continue;
                    var pair = list[first] < list[second] ? (list[first], list[second]) : (list[second], list[first]);
                    if (!counted.Add(pair))
                        continue;
                    if (Intersects(a.Start, a.End, b.Start, b.End))
                        crossings += a.Weight * b.Weight;
                }
            }
        }

        var linesThroughNodes = 0;
        for (var index = 0; index < segments.Count; index++)
        {
            var segment = segments[index];
            for (var vertex = 0; vertex < result.Boundaries.Count; vertex++)
            {
                if (vertex == segment.Source || vertex == segment.Target)
                    continue;
                if (CrossesRect(segment.Start, segment.End, Shrink(result.Boundaries[vertex], NodeMargin)))
                    linesThroughNodes++;
            }
        }

        return new LayoutQuality(crossings, linesThroughNodes, bends, length);
    }

    readonly record struct Segment(int Edge, int Source, int Target, Pos Start, Pos End, double Weight);

    // Lines of the same node fan out from (or into) the same anchor; those meeting there are not
    // crossings.
    static bool SharesEndpoint(Segment a, Segment b) =>
        a.Start == b.Start || a.Start == b.End || a.End == b.Start || a.End == b.End;

    static Rect Bounds(Segment segment) =>
        new(
            Math.Min(segment.Start.X, segment.End.X),
            Math.Min(segment.Start.Y, segment.End.Y),
            Math.Abs(segment.End.X - segment.Start.X),
            Math.Abs(segment.End.Y - segment.Start.Y)
        );

    static IEnumerable<(int, int)> Cells(Rect bounds)
    {
        var x1 = (int)Math.Floor(bounds.X / CellSize);
        var x2 = (int)Math.Floor((bounds.X + bounds.Width) / CellSize);
        var y1 = (int)Math.Floor(bounds.Y / CellSize);
        var y2 = (int)Math.Floor((bounds.Y + bounds.Height) / CellSize);
        for (var x = x1; x <= x2; x++)
        for (var y = y1; y <= y2; y++)
            yield return (x, y);
    }

    static double Distance(Pos a, Pos b) => Math.Sqrt((b.X - a.X) * (b.X - a.X) + (b.Y - a.Y) * (b.Y - a.Y));

    static Rect Shrink(Rect rect, double margin) =>
        new(
            rect.X + margin,
            rect.Y + margin,
            Math.Max(0, rect.Width - 2 * margin),
            Math.Max(0, rect.Height - 2 * margin)
        );

    // Proper intersection of two segments (touching at an end does not count).
    static bool Intersects(Pos a1, Pos a2, Pos b1, Pos b2)
    {
        var d1 = Cross(b1, b2, a1);
        var d2 = Cross(b1, b2, a2);
        var d3 = Cross(a1, a2, b1);
        var d4 = Cross(a1, a2, b2);
        return ((d1 > 0 && d2 < 0) || (d1 < 0 && d2 > 0)) && ((d3 > 0 && d4 < 0) || (d3 < 0 && d4 > 0));
    }

    static double Cross(Pos origin, Pos a, Pos b) =>
        (a.X - origin.X) * (b.Y - origin.Y) - (a.Y - origin.Y) * (b.X - origin.X);

    // Whether the segment passes through the rect's inside (Liang-Barsky clipping).
    static bool CrossesRect(Pos start, Pos end, Rect rect)
    {
        if (rect.Width <= 0 || rect.Height <= 0)
            return false;
        var dx = end.X - start.X;
        var dy = end.Y - start.Y;
        var t0 = 0.0;
        var t1 = 1.0;
        (double P, double Q)[] edges =
        [
            (-dx, start.X - rect.X),
            (dx, rect.X + rect.Width - start.X),
            (-dy, start.Y - rect.Y),
            (dy, rect.Y + rect.Height - start.Y),
        ];
        foreach (var (p, q) in edges)
        {
            if (p == 0)
            {
                if (q <= 0)
                    return false;
                continue;
            }
            var t = q / p;
            if (p < 0)
                t0 = Math.Max(t0, t);
            else
                t1 = Math.Min(t1, t);
            if (t0 >= t1)
                return false;
        }
        return true;
    }
}
