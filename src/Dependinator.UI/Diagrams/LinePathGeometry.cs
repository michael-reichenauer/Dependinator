using Dependinator.UI.Modeling.Models;
using Dependinator.UI.Shared.Types;

namespace Dependinator.UI.Diagrams;

static class LinePathGeometry
{
    // How a line's endpoints relate in the node tree; this decides which node's child-local
    // coordinate space the line is expressed and rendered in (its "owner").
    enum LineRelation
    {
        Direct, // Rendered inside an explicit RenderAncestor, crossing container levels
        ParentToChild, // From a container to one of its children
        ChildToParent, // From a child to its container
        Siblings, // Between two children of the same container
        Unrelated, // Nothing to render (no common coordinate space)
    }

    static LineRelation Classify(Line line) =>
        line.IsDirect ? LineRelation.Direct
        : line.Target.Parent == line.Source ? LineRelation.ParentToChild
        : line.Source.Parent == line.Target ? LineRelation.ChildToParent
        : line.Source.Parent == line.Target.Parent ? LineRelation.Siblings
        : LineRelation.Unrelated;

    // The node whose children-local coordinate space the line's segment points live in.
    public static bool TryGetOwnerNode(Line line, out Node owner)
    {
        owner = Classify(line) switch
        {
            LineRelation.Direct => line.RenderAncestor!,
            LineRelation.ParentToChild => line.Source,
            LineRelation.ChildToParent => line.Target,
            LineRelation.Siblings => line.Source.Parent,
            _ => null!,
        };
        return owner is not null;
    }

    public static bool TryGetLocalEndpoints(Line line, out LineEndpoints endpoints)
    {
        switch (Classify(line))
        {
            case LineRelation.Direct:
                return TryGetDirectLocalEndpoints(line, out endpoints);

            // A parent endpoint must be expressed in the parent's own child-local space: its
            // edge points (left edge / right edge at mid height) are mapped from boundary
            // coordinates by undoing the container transform (subtract ContainerOffset, divide
            // by ContainerZoom).
            case LineRelation.ParentToChild:
            {
                var parent = line.Source;
                var targetPreference = line.HasInheritanceTargetEnd
                    ? AnchorPreference.Bottom
                    : AnchorPreference.Default;
                var targetAnchor = NodeAnchors.GetLineAnchor(line.Target, LineAnchorRole.Target, targetPreference);
                endpoints = new LineEndpoints(
                    -parent.ContainerOffset.X / parent.ContainerZoom, // Parent's left edge
                    (parent.Boundary.Height / 2 - parent.ContainerOffset.Y) / parent.ContainerZoom, // Mid height
                    targetAnchor.X,
                    targetAnchor.Y
                );
                return true;
            }

            case LineRelation.ChildToParent:
            {
                var parent = line.Target;
                var sourcePreference = line.HasInheritanceSourceEnd ? AnchorPreference.Top : AnchorPreference.Default;
                var sourceAnchor = NodeAnchors.GetLineAnchor(line.Source, LineAnchorRole.Source, sourcePreference);
                endpoints = new LineEndpoints(
                    sourceAnchor.X,
                    sourceAnchor.Y,
                    (parent.Boundary.Width - parent.ContainerOffset.X) / parent.ContainerZoom, // Right edge
                    (parent.Boundary.Height / 2 - parent.ContainerOffset.Y) / parent.ContainerZoom // Mid height
                );
                return true;
            }

            case LineRelation.Siblings:
            {
                var sourcePreference = line.HasInheritanceSourceEnd ? AnchorPreference.Top : AnchorPreference.Default;
                var targetPreference = line.HasInheritanceTargetEnd
                    ? AnchorPreference.Bottom
                    : AnchorPreference.Default;
                var sourceAnchor = NodeAnchors.GetLineAnchor(line.Source, LineAnchorRole.Source, sourcePreference);
                var targetAnchor = NodeAnchors.GetLineAnchor(line.Target, LineAnchorRole.Target, targetPreference);
                endpoints = new LineEndpoints(sourceAnchor.X, sourceAnchor.Y, targetAnchor.X, targetAnchor.Y);
                return true;
            }

            default:
                endpoints = default;
                return false;
        }
    }

    // The clamped projection factor t ∈ [0,1] of point onto the segment start→end
    // (0 for a degenerate zero-length segment).
    public static double ProjectionFactor(Pos point, Pos start, Pos end)
    {
        var dx = end.X - start.X;
        var dy = end.Y - start.Y;
        var len2 = dx * dx + dy * dy;
        if (len2 == 0)
            return 0;

        var t = ((point.X - start.X) * dx + (point.Y - start.Y) * dy) / len2;
        return Math.Clamp(t, 0, 1);
    }

    // The point on the segment start→end closest to the given point.
    public static Pos ProjectPointOnSegment(Pos point, Pos start, Pos end)
    {
        var t = ProjectionFactor(point, start, end);
        return new Pos(start.X + (end.X - start.X) * t, start.Y + (end.Y - start.Y) * t);
    }

    static bool TryGetDirectLocalEndpoints(Line line, out LineEndpoints endpoints)
    {
        if (line.RenderAncestor is null)
        {
            endpoints = default;
            return false;
        }

        var (sourceAnchor, targetAnchor) = DirectLineCalculator.GetAnchorsRelativeToAncestor(
            line.RenderAncestor,
            line.Source,
            line.Target
        );

        endpoints = new LineEndpoints(sourceAnchor.X, sourceAnchor.Y, targetAnchor.X, targetAnchor.Y);
        return true;
    }

    public static LineEndpoints ToRendered(LineEndpoints local, Pos nodeCanvasPos, double childrenZoom) =>
        new(
            nodeCanvasPos.X + local.X1 * childrenZoom,
            nodeCanvasPos.Y + local.Y1 * childrenZoom,
            nodeCanvasPos.X + local.X2 * childrenZoom,
            nodeCanvasPos.Y + local.Y2 * childrenZoom
        );

    public static Pos ToRendered(Pos local, Pos nodeCanvasPos, double childrenZoom) =>
        new(nodeCanvasPos.X + local.X * childrenZoom, nodeCanvasPos.Y + local.Y * childrenZoom);

    // Whether the line runs "uphill" (up-right or down-left), used to place the line toolbar on
    // the correct side. Rendering scales both endpoints by the same positive zoom and offset, so
    // the local endpoints give the same orientation as the rendered line.
    public static bool IsUpHill(Line line)
    {
        if (!TryGetLocalEndpoints(line, out var endpoints))
            return false;

        return endpoints.X1 <= endpoints.X2 && endpoints.Y1 >= endpoints.Y2
            || endpoints.X1 >= endpoints.X2 && endpoints.Y1 <= endpoints.Y2;
    }

    public static IReadOnlyList<Pos> GetRenderedPolylinePoints(Line line, Pos nodeCanvasPos, double childrenZoom)
    {
        if (!TryGetLocalEndpoints(line, out var endpoints))
            return [];

        var points = new List<Pos>(line.SegmentPoints.Count + 2)
        {
            ToRendered(new Pos(endpoints.X1, endpoints.Y1), nodeCanvasPos, childrenZoom),
        };
        points.AddRange(line.SegmentPoints.Select(p => ToRendered(p, nodeCanvasPos, childrenZoom)));
        points.Add(ToRendered(new Pos(endpoints.X2, endpoints.Y2), nodeCanvasPos, childrenZoom));
        return points;
    }

    public readonly record struct LineEndpoints(double X1, double Y1, double X2, double Y2);
}
