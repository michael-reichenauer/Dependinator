using Dependinator.UI.Diagrams.Lines;
using Dependinator.UI.Modeling;
using Dependinator.UI.Modeling.Models;
using Dependinator.UI.Shared.Types;

// Walks the node tree and draws the part of it in one tile: each node as an icon, a summary or
// member card, or an open container with its children, lines (NodeLinesSvg) and annotations.
namespace Dependinator.UI.Diagrams.Svg;

static class NodeTreeSvg
{
    // Nodes smaller than this (in tile pixels) in both dimensions are skipped entirely,
    // including their whole subtree: they are invisible, but at far-out zoom containers can
    // hold hundreds of them, each otherwise emitting full icon markup. Lines to skipped nodes
    // still draw (see NodeLinesSvg.IsEndpointRendered), keeping the far-zoom line texture intact.
    const double MinRenderSize = 0.5;

    // Rendering lays out containers lazily (EnsureChildrenLayout) the first time their children
    // show, which can resize and open nodes the line representatives were resolved without; such
    // a render is redone with freshly synced reps (a few times at most, as each round can reveal
    // more containers to lay out).
    internal static string RenderRootContent(IModel model, double zoom, RenderContext rootContext)
    {
        const int MaxRenderRounds = 3;
        var content = "";
        for (var round = 0; round < MaxRenderRounds; round++)
        {
            RepLineService.Sync(model, zoom);
            var generation = NodeLayout.Generation;
            content = RenderNodeContent(model.Root, rootContext);
            if (NodeLayout.Generation == generation)
                break;
        }
        return content;
    }

    static string RenderNodeContent(Node node, RenderContext context)
    {
        EnsureChildrenLayout(node);

        var childrenCanvasOffset = CalculateChildrenCanvasOffset(node, context);
        var childrenZoom = context.Zoom * node.ContainerZoom;
        var childrenContext = context.With(childrenCanvasOffset, childrenZoom);

        // Zones are background: drawn first so the sibling lines and nodes lie on top of them,
        // with their selection chrome drawn last so the siblings cannot cover it.
        var zonesSvg = node
            .Children.Where(child => child.IsZone)
            .Select(child => RenderNode(child, childrenContext))
            .Where(svg => svg.Length > 0);
        var childNodeSvg = node
            .Children.Where(child => !child.IsZone)
            .Select(child => RenderNode(child, childrenContext))
            .Where(svg => svg.Length > 0);
        var linesSvg = NodeLinesSvg.RenderNodeLines(node, childrenCanvasOffset, childrenZoom, context);

        // The lines' labels draw above the child nodes: a short line's label would otherwise
        // end up under the node icons at its ends.
        var lineLabelsSvg = NodeLinesSvg.RenderNodeLineLabels(node, childrenCanvasOffset, childrenZoom, context);

        // Cousin and direct lines draw above the child nodes: they cross into containers whose
        // opaque fill would otherwise cover them.
        var directLinesSvg = NodeLinesSvg.RenderDirectNodeLines(node, childrenCanvasOffset, childrenZoom, context);
        var zoneOverlaysSvg = node
            .Children.Where(child => child.IsZone)
            .Select(child => RenderZoneOverlay(child, childrenContext))
            .Where(svg => svg.Length > 0);

        // A container's member rows sit on a card, drawn behind everything inside the container.
        var memberCardSvg =
            !node.IsRoot && NodeLayout.HasMemberChildren(node)
                ? MemberCardSvg.GetMemberCardSvg(
                    node,
                    childrenCanvasOffset,
                    childrenZoom,
                    row => IsRowShown(row, context),
                    // GetCardNodeSvg draws the card, except when the node is drawn without chrome
                    NodeViewPolicy.IsMemberCardNode(node)
                        && !NodeViewPolicy.IsRenderedFlat(context.Zoom)
                        && !NodeViewPolicy.IsTooLargeToBeSeen(node, context.Zoom)
                )
                : "";

        var privateToggleSvg =
            memberCardSvg.Length > 0
                ? MemberCardSvg.GetPrivateToggleSvg(
                    node,
                    childrenCanvasOffset,
                    childrenZoom,
                    row => IsRowShown(row, context)
                )
                : "";

        return new[] { memberCardSvg }
            .Where(svg => svg.Length > 0)
            .Concat(zonesSvg)
            .Concat(linesSvg)
            .Concat(childNodeSvg)
            .Append(privateToggleSvg)
            .Concat(lineLabelsSvg)
            .Concat(directLinesSvg)
            .Concat(zoneOverlaysSvg)
            .Join("\n");
    }

    // A selected zone's outline and resize handles, emitted after the siblings that are drawn
    // over the zone itself.
    static string RenderZoneOverlay(Node zone, RenderContext context) =>
        IsRendered(zone, context, out var geometry)
            ? NodeSvg.SelectedNodeSvg(zone, geometry.CanvasRect, context.Flags.IsEditing)
            : "";

    // Whether the node is drawn at all in this tile: not hidden (unless hidden nodes are revealed),
    // not a row kind the user left out, overlapping the tile, and at least MinRenderSize in one
    // dimension.
    static bool IsRendered(Node node, RenderContext context, out NodeGeometry geometry)
    {
        geometry = CalculateNodeGeometry(node, context);
        if (node.Visibility == NodeVisibility.Hidden && !context.Flags.RevealHidden)
            return false;
        if (context.Flags.Rows.Hides(node.Type))
            return false;
        if (MemberCard.IsFoldedRow(node))
            return false; // Drawn as its card's "+ N private" row
        if (!context.IsInTile(geometry.TileRect))
            return false;
        return geometry.TileRect.Width >= MinRenderSize || geometry.TileRect.Height >= MinRenderSize;
    }

    // A member row the card makes room for: not hidden by the user (unless revealed) or by View › Rows.
    static bool IsRowShown(Node row, RenderContext context) =>
        (row.Visibility != NodeVisibility.Hidden || context.Flags.RevealHidden) && !context.Flags.Rows.Hides(row.Type);

    static string RenderNode(Node node, RenderContext context)
    {
        if (!IsRendered(node, context, out var geometry))
            return "";

        // Annotations are leaves without children or chrome: a zone is a labeled background
        // rectangle, a note a circle. Short-circuit before the member/icon/container branches.
        if (node.IsZone)
            return ZoneSvg.GetZoneSvg(node, geometry.CanvasRect, context.Zoom);
        if (node.IsNote)
            return NoteSvg.GetNoteSvg(node, geometry.CanvasRect, context.Zoom);

        if (node.Type.IsMember)
            return MemberRowSvg.GetMemberNodeSvg(
                node,
                geometry.CanvasRect,
                context.Zoom,
                context.Flags.IsEditing,
                ProblemsOf(node, context)
            );

        if (node.IsPassThrough)
        { // An invisible container that covers its parent; render only its children, no chrome
            if (NodeViewPolicy.IsRenderedFlat(context.Zoom))
                return RenderFlattenedNodeContent(node, geometry, context);
            var passThroughContext = context.ForNestedContainer(geometry.TileRect);
            var passThroughContentSvg = RenderNodeContent(node, passThroughContext);
            return NodeSvg.GetTooLargeNodeContainerSvg(geometry.CanvasRect, passThroughContentSvg);
        }

        // A re-parse may have changed which rows fold since the card was laid out; otherwise an
        // edit may have moved the card's rows from under its header (NodeLayout.EnsureCardTransform).
        if (!node.IsChildrenLayoutRequired && NodeViewPolicy.IsMemberCardNode(node))
        {
            if (MemberCard.IsLayoutStale(node))
                node.IsChildrenLayoutRequired = true;
            else
                NodeLayout.EnsureCardTransform(node);
        }

        // The summary card decides by the rows' size, so lay out the rows it may switch to first
        // (a layout bumps NodeLayout.Generation, which redoes the render with synced lines).
        if (
            node.IsChildrenLayoutRequired
            && NodeViewPolicy.IsPastIcon(node, context.Zoom)
            && NodeViewPolicy.IsMemberCardNode(node)
        )
            EnsureChildrenLayout(node);

        if (NodeViewPolicy.IsMemberSummary(node, context.Zoom))
            return MemberCardSvg.GetMemberSummarySvg(
                node,
                geometry.CanvasRect,
                context.Zoom,
                context.Flags.IsEditing,
                ProblemsOf(node, context),
                HiddenChildCountOf(node, context),
                row => IsRowShown(row, context)
            );

        if (NodeViewPolicy.IsShowIcon(node, context.Zoom))
            return NodeSvg.GetNodeIconSvg(
                node,
                geometry.CanvasRect,
                context.Zoom,
                context.Flags.IsEditing,
                ProblemsOf(node, context),
                HiddenChildCountOf(node, context)
            );

        if (NodeViewPolicy.IsRenderedFlat(context.Zoom))
            return RenderFlattenedNodeContent(node, geometry, context);

        var nestedContext = context.ForNestedContainer(geometry.TileRect);
        var childrenContentSvg = RenderNodeContent(node, nestedContext);

        if (NodeViewPolicy.IsTooLargeToBeSeen(node, context.Zoom))
            return NodeSvg.GetTooLargeNodeContainerSvg(geometry.CanvasRect, childrenContentSvg);

        if (NodeViewPolicy.IsMemberCardNode(node))
            return MemberCardSvg.GetCardNodeSvg(
                node,
                geometry.CanvasRect,
                context.Zoom,
                context.Zoom * node.ContainerZoom,
                childrenContentSvg,
                context.Flags.IsEditing,
                ProblemsOf(node, context),
                HiddenChildCountOf(node, context)
            );

        return NodeSvg.GetNodeContainerSvg(
            node,
            geometry.CanvasRect,
            context.Zoom,
            childrenContentSvg,
            context.Flags.IsEditing,
            ProblemsOf(node, context),
            HiddenChildCountOf(node, context)
        );
    }

    static NodeProblems? ProblemsOf(Node node, RenderContext context) =>
        context.Flags.NodeProblems?.TryGetValue(node.Id, out var problems) == true ? problems : null;

    static int HiddenChildCountOf(Node node, RenderContext context) =>
        context.Flags.HiddenChildCounts?.TryGetValue(node.Id, out var count) == true ? count : 0;

    // At extreme zoom a nested svg viewport would carry offsets of millions of canvas units,
    // which browsers combine in single-precision floats, displacing the subtree far off-screen
    // (blank view). Rendering the children directly in the current viewport lets the offsets
    // telescope back to small numbers server-side in double precision.
    static string RenderFlattenedNodeContent(Node node, NodeGeometry geometry, RenderContext context)
    {
        var flatContext = context.With(new Pos(geometry.CanvasRect.X, geometry.CanvasRect.Y), context.Zoom);
        return RenderNodeContent(node, flatContext);
    }

    static NodeGeometry CalculateNodeGeometry(Node node, RenderContext context)
    {
        var canvasPos = new Pos(
            context.CanvasOffset.X + node.Boundary.X * context.Zoom,
            context.CanvasOffset.Y + node.Boundary.Y * context.Zoom
        );
        var width = node.Boundary.Width * context.Zoom;
        var height = node.Boundary.Height * context.Zoom;

        var canvasRect = new Rect(canvasPos.X, canvasPos.Y, width, height);
        var tilePos = new Pos(
            context.TilePosition.X + node.Boundary.X * context.Zoom + context.CanvasOffset.X,
            context.TilePosition.Y + node.Boundary.Y * context.Zoom + context.CanvasOffset.Y
        );
        var tileRect = new Rect(tilePos.X, tilePos.Y, width, height);

        return new NodeGeometry(canvasRect, tileRect);
    }

    static Pos CalculateChildrenCanvasOffset(Node node, RenderContext context) =>
        new(
            context.CanvasOffset.X + node.ContainerOffset.X * context.Zoom,
            context.CanvasOffset.Y + node.ContainerOffset.Y * context.Zoom
        );

    static void EnsureChildrenLayout(Node node)
    {
        if (node.IsChildrenLayoutRequired)
            NodeLayout.AdjustChildren(node);
    }

    readonly record struct NodeGeometry(Rect CanvasRect, Rect TileRect);
}
