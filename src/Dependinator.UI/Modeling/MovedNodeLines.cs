using Dependinator.UI.Modeling.Layout;
using Dependinator.UI.Modeling.Models;
using Dependinator.UI.Shared.Types;

namespace Dependinator.UI.Modeling;

// The line points to change after the user moved nodes, in the moved nodes' containers. The
// layout's bends (not IsSegmentsUserSet) follow the nodes: a line that touches a moved node, now
// passes through a node, or no longer needs all of its bends is routed again where the nodes
// stand now (FreeLineRouter). Bends the user set stay where they are, except on a line between
// two nodes that moved together: it moves along with them, bends and all.
static class MovedNodeLines
{
    // How far apart two moves may be and still count as moving together.
    const double SameMoveTolerance = 0.01;

    // The changed points of each line, for nodes moved by the given distances (in their
    // container's units).
    public static IReadOnlyDictionary<Line, IReadOnlyList<Pos>> NewPoints(IReadOnlyDictionary<Node, Pos> moves)
    {
        var changes = new Dictionary<Line, IReadOnlyList<Pos>>();
        var containers = moves.Keys.Where(node => !node.IsRoot).Select(node => node.Parent).Distinct();
        foreach (var container in containers)
        {
            var obstacles = LineRoutes.ObstaclesIn(container);
            // Where the container's moved nodes were and where they are now.
            var movedAreas = container
                .Children.Where(moves.ContainsKey)
                .SelectMany(node =>
                    new[]
                    {
                        node.Boundary,
                        node.Boundary with
                        {
                            X = node.Boundary.X - moves[node].X,
                            Y = node.Boundary.Y - moves[node].Y,
                        },
                    }
                )
                .ToList();
            foreach (var line in LineRoutes.SiblingLines(container))
            {
                if (
                    NewPoints(line, moves, obstacles, movedAreas) is { } points
                    && !points.SequenceEqual(line.SegmentPoints)
                )
                    changes[line] = points;
            }
        }
        return changes;
    }

    static IReadOnlyList<Pos>? NewPoints(
        Line line,
        IReadOnlyDictionary<Node, Pos> moves,
        FreeLineRouter.Obstacles nodes,
        List<Rect> movedAreas
    )
    {
        var isSourceMoved = moves.TryGetValue(line.Source, out var sourceMove);
        var isTargetMoved = moves.TryGetValue(line.Target, out var targetMove);
        var isMovedTogether = isSourceMoved && isTargetMoved && IsSame(sourceMove!, targetMove!);

        IReadOnlyList<Pos> points = isMovedTogether
            ? [.. line.SegmentPoints.Select(point => new Pos(point.X + sourceMove!.X, point.Y + sourceMove.Y))]
            : line.SegmentPoints;
        if (line.IsSegmentsUserSet)
            return isMovedTogether ? points : null;

        if (!LineRoutes.TryGetEnds(line, nodes, out var start, out var end, out var obstacles))
            return null;

        var isTouched = (isSourceMoved || isTargetMoved) && !isMovedTogether;
        // A line away from where the moved nodes were and are now is as clear as before.
        if (!isTouched && !IsNear([start, .. points, end], movedAreas))
            return isMovedTogether ? points : null;
        if (isTouched || FreeLineRouter.IsBlocked([start, .. points, end], obstacles))
            return FreeLineRouter.Route(start, end, obstacles);

        // A clear line keeps its bends unless it needs fewer now (a node it went around moved away).
        if (
            points.Count > 0
            && FreeLineRouter.Route(start, end, obstacles) is var routed
            && routed.Count < points.Count
            && !FreeLineRouter.IsBlocked([start, .. routed, end], obstacles)
        )
            return routed;
        return points;
    }

    // Whether the line's box, grown by the room a detour takes, overlaps one of the areas.
    static bool IsNear(IReadOnlyList<Pos> points, List<Rect> areas)
    {
        var minX = points.Min(point => point.X) - NearMargin;
        var minY = points.Min(point => point.Y) - NearMargin;
        var box = new Rect(
            minX,
            minY,
            points.Max(point => point.X) + NearMargin - minX,
            points.Max(point => point.Y) + NearMargin - minY
        );
        return areas.Any(area => area.Intersects(box));
    }

    // About as far beside a node as a line's bend around it goes.
    const double NearMargin = 3 * FreeLineRouter.Clearance + 50;

    static bool IsSame(Pos a, Pos b) =>
        Math.Abs(a.X - b.X) < SameMoveTolerance && Math.Abs(a.Y - b.Y) < SameMoveTolerance;
}
