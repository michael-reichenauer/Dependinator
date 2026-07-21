using Dependinator.UI.Diagrams.Tiles;
using Dependinator.UI.Modeling;
using Dependinator.UI.Modeling.Models;
using Dependinator.UI.Shared.Types;
using static System.FormattableString;

// Generates the SVG rendering of the diagram from the model, producing the tiled SVG content
// drawn on the canvas.
namespace Dependinator.UI.Diagrams.Svg;

interface ISvgService
{
    Tile GetTile(Rect viewRect, double zoom);

    // Renders the diagram content covering canvasRect (canvas coordinates) at zoom (canvas units
    // per output pixel), in output-pixel coordinates with origin at the rect's top-left. Returns
    // the bare content (no <svg> wrapper, no defs) for embedding in an export document. Unlike
    // GetTile, this renders fresh (no tile cache) and does not touch the view state.
    string GetContentSvg(Rect canvasRect, double zoom);
}

[Transient]
class SvgService : ISvgService
{
    readonly IModelMgr modelMgr;
    readonly ITilesMgr tilesMgr;

    public SvgService(IModelMgr modelMgr, ITilesMgr tilesMgr)
    {
        this.modelMgr = modelMgr;
        this.tilesMgr = tilesMgr;
    }

    public Tile GetTile(Rect viewRect, double zoom)
    {
        using (var model = modelMgr.UseModel())
        {
            if (model.Root.Children.Count == 0)
                return Tile.Empty;
            if (viewRect.Width == 0 || viewRect.Height == 0)
                return Tile.Empty;

            model.ViewRect = viewRect;
            model.Zoom = zoom;
        }

        TileKey tileKey;
        Tile tile;
        using (var tiles = tilesMgr.UseTiles())
        {
            if (tiles.TryGetLastUsed(viewRect, zoom, out tile))
                return tile; // Same tile as last call

            tileKey = TileKey.From(viewRect, zoom);
            if (tiles.TryGetCached(tileKey, viewRect, zoom, out tile))
                return tile;
        }

        // Create a new tile and cache it
        using (var model = modelMgr.UseModel())
        {
            tile = CreateModelTile(model, tileKey);
        }
        using (var tiles = tilesMgr.UseTiles())
        {
            tiles.SetCached(tile, viewRect, zoom);
        }

        return tile;
    }

    public string GetContentSvg(Rect canvasRect, double zoom)
    {
        using var model = modelMgr.UseModel();
        if (model.Root.Children.Count == 0 || canvasRect.Width <= 0 || canvasRect.Height <= 0 || zoom <= 0)
            return "";

        RepLineService.Sync(model, zoom);

        var offset = new Pos(-canvasRect.X / zoom, -canvasRect.Y / zoom);
        var bounds = new Rect(0, 0, canvasRect.Width / zoom, canvasRect.Height / zoom);
        var context = new RenderContext(offset, 1 / zoom, bounds, Pos.None);
        return RenderNodeContent(model.Root, context);
    }

    static Tile CreateModelTile(IModel model, TileKey tileKey)
    {
        var tileRect = tileKey.GetTileRect();
        var tileWithMargin = tileKey.GetTileRectWithMargin();
        var tileZoom = tileKey.GetTileZoom();
        var tileOffset = new Pos(-tileRect.X, -tileRect.Y);

        RepLineService.Sync(model, tileZoom);

        var rootContext = new RenderContext(tileOffset, 1 / tileZoom, tileWithMargin, Pos.None);
        var rootContentSvg = RenderNodeContent(model.Root, rootContext);

        // Enable this if need to show tile border and/or tile with margin border
        // var tileBorderSvg = $"""<rect x="{0}" y="{0}" width="{tileRect.Width:0.##}" height="{tileRect.Height:0.##}" stroke-width="{3}" rx="5" fill="none" stroke="red"/>""";
        // var tileMarginBorderSvg = $"""<rect x="{tileWithMargin.X}" y="{tileWithMargin.Y}" width="{tileWithMargin.Width:0.##}" height="{tileWithMargin.Height:0.##}" stroke-width="{3}" rx="5" fill="none" stroke="green"/>""";

        var (x, y, w, h) = tileKey.GetViewRect();
        var tileViewBox = Invariant($"{x:0.##} {y:0.##} {w:0.##} {h:0.##}");
        var tileSvg = Invariant(
            $"""
            <svg width="{w:0.##}" height="{h:0.##}" viewBox="{tileViewBox}" xmlns="http://www.w3.org/2000/svg">
              {rootContentSvg}
            </svg>
            """
        );

        return new Tile(tileKey, tileSvg, tileZoom, tileOffset);
    }

    static string RenderNodeContent(Node node, RenderContext context)
    {
        EnsureChildrenLayout(node);

        var childrenCanvasOffset = CalculateChildrenCanvasOffset(node, context);
        var childrenZoom = context.Zoom * node.ContainerZoom;
        var childrenContext = context.With(childrenCanvasOffset, childrenZoom);

        var childNodeSvg = node
            .Children.Select(child => RenderNode(child, childrenContext))
            .Where(svg => svg.Length > 0);
        var linesSvg = RenderNodeLines(node, childrenCanvasOffset, childrenZoom, context);

        // Cousin and direct lines draw above the child nodes: they cross into containers whose
        // opaque fill would otherwise cover them.
        var directLinesSvg = RenderDirectNodeLines(node, childrenCanvasOffset, childrenZoom, context);

        return linesSvg.Concat(childNodeSvg).Concat(directLinesSvg).Join("\n");
    }

    static string RenderNode(Node node, RenderContext context)
    {
        if (node.IsHidden && !ViewOptions.ShowHiddenNodes)
            return "";

        var geometry = CalculateNodeGeometry(node, context);

        if (!RectOverlap(context.TileBounds, geometry.TileRect))
            return "";

        // A note is a leaf annotation drawn as a circle; it has no children or chrome, so short-
        // circuit before the member/icon/container branches.
        if (node.IsNote)
            return NoteSvg.GetNoteSvg(node, geometry.CanvasRect, context.Zoom);

        if (node.Type.IsMember)
            return NodeSvg.GetMemberNodeSvg(node, geometry.CanvasRect, context.Zoom);

        if (node.IsPassThrough)
        { // An invisible container that covers its parent; render only its children, no chrome
            var passThroughContext = context.ForNestedContainer(geometry.TileRect);
            var passThroughContentSvg = RenderNodeContent(node, passThroughContext);
            return NodeSvg.GetTooLargeNodeContainerSvg(geometry.CanvasRect, passThroughContentSvg);
        }

        if (NodeViewPolicy.IsShowIcon(node.Type, context.Zoom))
            return NodeSvg.GetNodeIconSvg(node, geometry.CanvasRect, context.Zoom);

        var nestedContext = context.ForNestedContainer(geometry.TileRect);
        var childrenContentSvg = RenderNodeContent(node, nestedContext);

        if (NodeViewPolicy.IsTooLargeToBeSeen(context.Zoom))
            return NodeSvg.GetTooLargeNodeContainerSvg(geometry.CanvasRect, childrenContentSvg);

        return NodeSvg.GetNodeContainerSvg(node, geometry.CanvasRect, context.Zoom, childrenContentSvg);
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

    static bool RectOverlap(Rect first, Rect second)
    {
        if (first.X + first.Width <= second.X || second.X + second.Width <= first.X)
            return false;
        if (first.Y + first.Height <= second.Y || second.Y + second.Height <= first.Y)
            return false;
        return true;
    }

    static IEnumerable<string> RenderNodeLines(Node node, Pos nodeCanvasPos, double childrenZoom, RenderContext context)
    {
        // Parent-to-child segments are the fan-out of incoming links inside this container
        // (and direct parent-to-child links) — crossing rep lines end at the container, and
        // these continue inside.
        var parentToChildrenLines = node.SourceLines.Where(l => l.Target.Parent == node);
        foreach (var line in parentToChildrenLines)
        {
            if (line.IsSplitSuppressed)
                continue; // Temporarily replaced by its user-split lines (see DependenciesService)
            if (line.IsHidden && !ViewOptions.ShowHiddenNodes)
                continue;
            if (line.Target.IsPassThrough)
                continue; // The pass-through node covers this parent, so the segment is degenerate
            if (!IsEitherEndpointRendered(line, node, nodeCanvasPos, childrenZoom, context))
                continue;
            yield return LineSvg.GetLineSvg(line, nodeCanvasPos, childrenZoom);
        }

        // All sibling lines and children to parent lines
        foreach (var child in node.Children)
        {
            foreach (var line in child.SourceLines)
            {
                if (!line.IsActiveRep)
                    continue; // Only current representative lines are drawn (see RepLineService)
                if (line.IsSplitSuppressed)
                    continue; // Temporarily replaced by its user-split lines (see DependenciesService)
                if (line.Target.Parent == line.Source)
                    continue;
                if (line.IsHidden && !ViewOptions.ShowHiddenNodes)
                    continue;
                if (line.Source.IsPassThrough && line.Target == node)
                    continue; // The pass-through node covers this parent, so the segment is degenerate
                if (!IsEitherEndpointRendered(line, node, nodeCanvasPos, childrenZoom, context))
                    continue;
                yield return LineSvg.GetLineSvg(line, nodeCanvasPos, childrenZoom);
            }
        }
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
        if (endpoint.IsHidden && !ViewOptions.ShowHiddenNodes)
            return false;

        // The endpoint's tile rect, as RenderNode computes it for this container's children
        // (see CalculateNodeGeometry with the children context).
        var tileRect = new Rect(
            context.TilePosition.X + nodeCanvasPos.X + endpoint.Boundary.X * childrenZoom,
            context.TilePosition.Y + nodeCanvasPos.Y + endpoint.Boundary.Y * childrenZoom,
            endpoint.Boundary.Width * childrenZoom,
            endpoint.Boundary.Height * childrenZoom
        );
        return RectOverlap(context.TileBounds, tileRect);
    }

    static IEnumerable<string> RenderDirectNodeLines(
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
            if (directLine.IsSplitSuppressed)
                continue; // Temporarily replaced by its user-split lines (see DependenciesService)
            if (directLine.IsHidden && !ViewOptions.ShowHiddenNodes)
                continue;
            if (!IsEitherDirectEndpointRendered(directLine, node, nodeCanvasPos, childrenZoom, context))
                continue;
            var svg = LineSvg.GetDirectLineSvg(directLine, node, nodeCanvasPos, childrenZoom);
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
        if (endpoint.IsHidden && !ViewOptions.ShowHiddenNodes)
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
        return RectOverlap(context.TileBounds, tileRect);
    }

    readonly record struct RenderContext(Pos CanvasOffset, double Zoom, Rect TileBounds, Pos TilePosition)
    {
        public RenderContext With(Pos canvasOffset, double zoom) => new(canvasOffset, zoom, TileBounds, TilePosition);

        public RenderContext ForNestedContainer(Rect tileRect) =>
            new(Pos.None, Zoom, TileBounds, new Pos(tileRect.X, tileRect.Y));
    }

    readonly record struct NodeGeometry(Rect CanvasRect, Rect TileRect);
}
