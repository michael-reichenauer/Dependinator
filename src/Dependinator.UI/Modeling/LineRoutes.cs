using Dependinator.UI.Diagrams.Lines;
using Dependinator.UI.Modeling.Layout;
using Dependinator.UI.Modeling.Models;
using Dependinator.UI.Shared.Types;

namespace Dependinator.UI.Modeling;

// Routes the lines between a container's children again where the nodes stand now
// (FreeLineRouter), keeping the nodes where they are: after a move (MovedNodeLines), for one
// line ("Re-route line" on the line toolbar) or for all lines in a container ("Reset Lines").
static class LineRoutes
{
    // The new points of a line between siblings, or null when the line is not one the layout
    // routes (IsRoutable) or its ends are not placed yet.
    public static IReadOnlyList<Pos>? Route(Line line) =>
        IsRoutable(line) ? Route(line, ObstaclesIn(line.Source.Parent)) : null;

    // The new points of each line between the container's children that are not already so.
    public static IReadOnlyDictionary<Line, IReadOnlyList<Pos>> RouteAll(Node container)
    {
        var changes = new Dictionary<Line, IReadOnlyList<Pos>>();
        var obstacles = ObstaclesIn(container);
        foreach (var line in SiblingLines(container))
        {
            if (Route(line, obstacles) is { } points && !points.SequenceEqual(line.SegmentPoints))
                changes[line] = points;
        }
        return changes;
    }

    // The children a line between siblings must go around.
    public static FreeLineRouter.Obstacles ObstaclesIn(Node container) =>
        new([
            .. container
                .Children.Where(child =>
                    NodeLayout.IsArranged(child)
                    && !child.IsPassThrough
                    && child.Boundary != Rect.None
                    && child.Visibility != NodeVisibility.Hidden
                )
                .Select(child => child.Boundary),
        ]);

    // The routable lines between the container's children (IsRoutable).
    public static IEnumerable<Line> SiblingLines(Node container) =>
        container
            .Children.SelectMany(child => child.SourceLines)
            .Where(line => line.Target.Parent == container && IsRoutable(line));

    // A line between siblings whose points live in their container's space and that the layout
    // routes (see LayoutSnapshot.IsLayoutLine); a member card's rows keep their lines as the
    // card lays them out (no bends).
    public static bool IsRoutable(Line line) =>
        !line.Source.IsRoot
        && line.Target.Parent == line.Source.Parent
        && line.Target != line.Source
        && !line.IsDirect
        && !line.IsCousin
        && !line.IsEmpty
        && !line.Source.Type.IsMember
        && !line.Target.Type.IsMember
        && line.Visibility != NodeVisibility.Hidden;

    // The line routed from its source to its target around the obstacles (but its own ends), or
    // null when its ends are not placed yet.
    public static IReadOnlyList<Pos>? Route(Line line, FreeLineRouter.Obstacles nodes)
    {
        if (!TryGetEnds(line, nodes, out var start, out var end, out var obstacles))
            return null;
        return FreeLineRouter.Route(start, end, obstacles);
    }

    // The line's ends and the obstacles it must go around (all but its own ends).
    public static bool TryGetEnds(
        Line line,
        FreeLineRouter.Obstacles nodes,
        out Pos start,
        out Pos end,
        out FreeLineRouter.Obstacles obstacles
    )
    {
        (start, end, obstacles) = (Pos.None, Pos.None, nodes);
        if (!LinePathGeometry.TryGetLocalEndpoints(line, out var ends))
            return false;
        start = new Pos(ends.X1, ends.Y1);
        end = new Pos(ends.X2, ends.Y2);
        obstacles = nodes.Without(line.Source.Boundary, line.Target.Boundary);
        return true;
    }
}
