using Dependinator.Diagrams.Svg;
using Dependinator.Models;

namespace Dependinator.Diagrams;

static class LinePathGeometry
{
    public static bool TryGetOwnerNode(Line line, out Node owner)
    {
        if (line.IsDirect)
        {
            owner = line.RenderAncestor!;
            return owner is not null;
        }

        if (line.Target.Parent == line.Source)
        {
            owner = line.Source;
            return true;
        }

        if (line.Source.Parent == line.Target)
        {
            owner = line.Target;
            return true;
        }

        owner = line.Source.Parent;
        return owner == line.Target.Parent;
    }

    public static bool TryGetLocalEndpoints(Line line, out LineEndpoints endpoints)
    {
        if (line.IsDirect)
            return TryGetDirectLocalEndpoints(line, out endpoints);

        var sourceAnchor = NodeSvg.GetLineAnchor(line.Source, NodeSvg.LineAnchorRole.Source);
        var targetAnchor = NodeSvg.GetLineAnchor(line.Target, NodeSvg.LineAnchorRole.Target);

        if (line.Target.Parent == line.Source)
        {
            var parent = line.Source;
            endpoints = new LineEndpoints(
                -parent.ContainerOffset.X / parent.ContainerZoom,
                (parent.Boundary.Height / 2 - parent.ContainerOffset.Y) / parent.ContainerZoom,
                targetAnchor.X,
                targetAnchor.Y
            );
            return true;
        }

        if (line.Source.Parent == line.Target)
        {
            var parent = line.Target;
            endpoints = new LineEndpoints(
                sourceAnchor.X,
                sourceAnchor.Y,
                (parent.Boundary.Width - parent.ContainerOffset.X) / parent.ContainerZoom,
                (parent.Boundary.Height / 2 - parent.ContainerOffset.Y) / parent.ContainerZoom
            );
            return true;
        }

        if (line.Source.Parent != line.Target.Parent)
        {
            endpoints = default;
            return false;
        }

        endpoints = new LineEndpoints(sourceAnchor.X, sourceAnchor.Y, targetAnchor.X, targetAnchor.Y);
        return true;
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
