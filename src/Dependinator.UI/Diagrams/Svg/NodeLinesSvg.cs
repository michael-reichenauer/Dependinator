using Dependinator.UI.Diagrams.Lines;
using Dependinator.UI.Modeling.Models;
using Dependinator.UI.Shared.Types;
using static Dependinator.UI.Diagrams.Svg.LineEmphasis;

// The lines an open container draws in one tile: its sibling lines and fan-out segments, their
// labels, and the direct and cousin lines that cross into nested containers, each styled by
// LineEmphasis and drawn by LineSvg.
namespace Dependinator.UI.Diagrams.Svg;

static class NodeLinesSvg
{
    // The sibling lines and the fan-out segments of this container; their labels go into a layer
    // of their own above the child nodes (see RenderNodeContent), so the lines are drawn without.
    internal static IEnumerable<string> RenderNodeLines(
        Node node,
        Pos nodeCanvasPos,
        double childrenZoom,
        RenderContext context
    )
    {
        foreach (var (line, isCyclic) in DrawnNodeLines(node, nodeCanvasPos, childrenZoom, context))
        {
            yield return LineSvg.GetLineSvg(
                line,
                nodeCanvasPos,
                childrenZoom,
                IsLineDimmed(line, context),
                isCyclic,
                IsLineOnPath(line, context),
                IsLineViolating(line, context),
                IsLineWeighted(line, context),
                includeLabel: false,
                isLit: IsLineLit(line, context)
            );
        }
    }

    internal static IEnumerable<string> RenderNodeLineLabels(
        Node node,
        Pos nodeCanvasPos,
        double childrenZoom,
        RenderContext context
    )
    {
        foreach (var (line, _) in DrawnNodeLines(node, nodeCanvasPos, childrenZoom, context))
        {
            var labelSvg = LineSvg.GetLineLabelSvg(
                line,
                nodeCanvasPos,
                childrenZoom,
                IsLineDimmed(line, context),
                IsLineLit(line, context)
            );
            if (labelSvg.Length > 0)
                yield return labelSvg;
        }
    }

    // The lines of this container that are drawn in this tile, with whether each is cyclic.
    // Stubs come last, so they are drawn on top: a sibling line can run across a stub (a line
    // back to a node on the left starts at its node's right side and passes under the node and
    // the stub into it), and the stub is the short one of the two, so it gets the click and the
    // hover.
    static IEnumerable<(Line Line, bool IsCyclic)> DrawnNodeLines(
        Node node,
        Pos nodeCanvasPos,
        double childrenZoom,
        RenderContext context
    )
    {
        // Parent-to-child segments are the fan-out of incoming links inside this container
        // (and direct parent-to-child links); only the segments below the links' current
        // target representative are active (see RepLineService).
        List<(Line Line, bool IsCyclic)>? stubs = null;
        var parentToChildrenLines = node.SourceLines.Where(l => l.Target.Parent == node);
        foreach (var line in parentToChildrenLines)
        {
            if (!line.IsActiveRep)
                continue; // Only current representative segments are drawn (see RepLineService)
            if (line.Visibility == NodeVisibility.Hidden && !context.Flags.RevealHidden)
                continue;
            if (line.Target.IsPassThrough)
                continue; // The pass-through node covers this parent, so the segment is degenerate
            if (IsLineFilteredOut(line, context.Flags.Filter, context.Flags.Rows))
                continue;
            if (!IsEitherEndpointRendered(line, node, nodeCanvasPos, childrenZoom, context))
                continue;
            if (LineStub.IsStub(line, IsLineOnPath(line, context)))
                (stubs ??= []).Add((line, false));
            else
                yield return (line, false);
        }

        // All sibling lines and children to parent lines
        foreach (var child in node.Children)
        {
            foreach (var line in child.SourceLines)
            {
                if (!line.IsActiveRep)
                    continue; // Only current representative lines are drawn (see RepLineService)
                if (line.Target.Parent == line.Source)
                    continue;
                if (line.Visibility == NodeVisibility.Hidden && !context.Flags.RevealHidden)
                    continue;
                if (line.Source.IsPassThrough && line.Target == node)
                    continue; // The pass-through node covers this parent, so the segment is degenerate
                if (IsLineFilteredOut(line, context.Flags.Filter, context.Flags.Rows))
                    continue;
                if (!IsEitherEndpointRendered(line, node, nodeCanvasPos, childrenZoom, context))
                    continue;
                if (LineStub.IsStub(line, IsLineOnPath(line, context)))
                    (stubs ??= []).Add((line, IsLineCyclic(line, context)));
                else
                    yield return (line, IsLineCyclic(line, context));
            }
        }

        if (stubs is null)
            yield break;
        foreach (var stub in stubs)
            yield return stub;
    }

    // A line is drawn while at least one of its endpoint nodes is rendered in this tile; a line
    // whose both endpoints are invisible is noise the viewer cannot interpret. The container
    // itself always counts as rendered, since this tile is rendering its content.
    static bool IsEitherEndpointRendered(
        Line line,
        Node node,
        Pos nodeCanvasPos,
        double childrenZoom,
        RenderContext context
    ) =>
        IsEndpointRendered(line.Source, node, nodeCanvasPos, childrenZoom, context)
        || IsEndpointRendered(line.Target, node, nodeCanvasPos, childrenZoom, context);

    static bool IsEndpointRendered(
        Node endpoint,
        Node node,
        Pos nodeCanvasPos,
        double childrenZoom,
        RenderContext context
    )
    {
        if (endpoint == node)
            return true;
        if (endpoint.Visibility == NodeVisibility.Hidden && !context.Flags.RevealHidden)
            return false;
        if (context.Flags.Rows.Hides(endpoint.Type))
            return false;

        // The endpoint's tile rect, as RenderNode computes it for this container's children
        // (see CalculateNodeGeometry with the children context).
        var tileRect = new Rect(
            context.TilePosition.X + nodeCanvasPos.X + endpoint.Boundary.X * childrenZoom,
            context.TilePosition.Y + nodeCanvasPos.Y + endpoint.Boundary.Y * childrenZoom,
            endpoint.Boundary.Width * childrenZoom,
            endpoint.Boundary.Height * childrenZoom
        );
        return context.IsInTile(tileRect);
    }

    internal static IEnumerable<string> RenderDirectNodeLines(
        Node node,
        Pos nodeCanvasPos,
        double childrenZoom,
        RenderContext context
    )
    {
        foreach (var directLine in node.DirectLines)
        {
            if (directLine.IsCousin && !directLine.IsActiveRep)
                continue; // An inactive cousin line kept only for its user waypoints/description
            if (directLine.Visibility == NodeVisibility.Hidden && !context.Flags.RevealHidden)
                continue;
            if (IsLineFilteredOut(directLine, context.Flags.Filter, context.Flags.Rows))
                continue;
            if (!IsEitherDirectEndpointRendered(directLine, node, nodeCanvasPos, childrenZoom, context))
                continue;
            var svg = LineSvg.GetDirectLineSvg(
                directLine,
                node,
                nodeCanvasPos,
                childrenZoom,
                IsLineDimmed(directLine, context),
                IsLineOnPath(directLine, context),
                IsLineViolating(directLine, context),
                IsLineWeighted(directLine, context),
                IsLineLit(directLine, context)
            );
            if (svg.Length > 0)
                yield return svg;
        }
    }

    // Direct/cousin lines follow the same endpoint-visibility rule as ordinary lines: drawn
    // while the source or target node is rendered in this tile. Their endpoints can lie many
    // levels below the render ancestor, so the endpoint rect is mapped from its global
    // position into the ancestor's children-local space (as DirectLineCalculator does for the
    // anchors). Lines that merely pass over the view (both endpoints invisible) stay dropped —
    // at deep zoom many cousin lines exist model-wide, and rendering every crossing makes
    // tiles enormous.
    static bool IsEitherDirectEndpointRendered(
        Line line,
        Node ancestor,
        Pos nodeCanvasPos,
        double childrenZoom,
        RenderContext context
    ) =>
        IsDirectEndpointRendered(line.Source, ancestor, nodeCanvasPos, childrenZoom, context)
        || IsDirectEndpointRendered(line.Target, ancestor, nodeCanvasPos, childrenZoom, context);

    static bool IsDirectEndpointRendered(
        Node endpoint,
        Node ancestor,
        Pos nodeCanvasPos,
        double childrenZoom,
        RenderContext context
    )
    {
        if (endpoint == ancestor)
            return true;
        if (endpoint.Visibility == NodeVisibility.Hidden && !context.Flags.RevealHidden)
            return false;
        if (context.Flags.Rows.Hides(endpoint.Type))
            return false;

        var (endpointPos, endpointZoom) = endpoint.GetPosAndZoom();
        var (ancestorPos, ancestorZoom) = ancestor.GetPosAndZoom();
        var childrenScale = ancestorZoom * ancestor.ContainerZoom;
        if (childrenScale <= 0)
            return true;

        var localX = (endpointPos.X - ancestorPos.X - ancestor.ContainerOffset.X * ancestorZoom) / childrenScale;
        var localY = (endpointPos.Y - ancestorPos.Y - ancestor.ContainerOffset.Y * ancestorZoom) / childrenScale;
        var localZoom = endpointZoom / childrenScale;

        var tileRect = new Rect(
            context.TilePosition.X + nodeCanvasPos.X + localX * childrenZoom,
            context.TilePosition.Y + nodeCanvasPos.Y + localY * childrenZoom,
            endpoint.Boundary.Width * localZoom * childrenZoom,
            endpoint.Boundary.Height * localZoom * childrenZoom
        );
        return context.IsInTile(tileRect);
    }
}
