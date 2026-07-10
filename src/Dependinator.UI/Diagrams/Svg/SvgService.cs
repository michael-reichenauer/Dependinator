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

    static Tile CreateModelTile(IModel model, TileKey tileKey)
    {
        var tileRect = tileKey.GetTileRect();
        var tileWithMargin = tileKey.GetTileRectWithMargin();
        var tileZoom = tileKey.GetTileZoom();
        var tileOffset = new Pos(-tileRect.X, -tileRect.Y);

        var rootContext = new RenderContext(tileOffset, 1 / tileZoom, tileWithMargin, Pos.Zero);
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
        var linesSvg = RenderNodeLines(node, childrenCanvasOffset, context.Zoom, childrenZoom);
        var directLinesSvg = RenderDirectNodeLines(node, childrenCanvasOffset, childrenZoom);

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
            return NodeSvg.GetToLargeNodeContainerSvg(geometry.CanvasRect, passThroughContentSvg);
        }

        if (NodeViewPolicy.IsShowIcon(node.Type, context.Zoom))
            return NodeSvg.GetNodeIconSvg(node, geometry.CanvasRect, context.Zoom);

        var nestedContext = context.ForNestedContainer(geometry.TileRect);
        var childrenContentSvg = RenderNodeContent(node, nestedContext);

        if (NodeViewPolicy.IsToLargeToBeSeen(context.Zoom))
            return NodeSvg.GetToLargeNodeContainerSvg(geometry.CanvasRect, childrenContentSvg);

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

    static IEnumerable<string> RenderNodeLines(Node node, Pos nodeCanvasPos, double parentZoom, double childrenZoom)
    {
        var parentToChildrenLines = node.SourceLines.Where(l => l.Target.Parent == node);
        foreach (var line in parentToChildrenLines)
        {
            if (line.IsHidden && !ViewOptions.ShowHiddenNodes)
                continue;
            if (line.Target.IsPassThrough)
                continue; // The pass-through node covers this parent, so the segment is degenerate
            yield return LineSvg.GetLineSvg(line, nodeCanvasPos, parentZoom, childrenZoom);
        }

        // All sibling lines and children to parent lines
        foreach (var child in node.Children)
        {
            foreach (var line in child.SourceLines)
            {
                if (line.Target.Parent == line.Source)
                    continue;
                if (line.IsHidden && !ViewOptions.ShowHiddenNodes)
                    continue;
                if (line.Source.IsPassThrough && line.Target == node)
                    continue; // The pass-through node covers this parent, so the segment is degenerate
                yield return LineSvg.GetLineSvg(line, nodeCanvasPos, parentZoom, childrenZoom);
            }
        }
    }

    static IEnumerable<string> RenderDirectNodeLines(Node node, Pos nodeCanvasPos, double childrenZoom)
    {
        foreach (var directLine in node.DirectLines)
        {
            if (directLine.IsHidden && !ViewOptions.ShowHiddenNodes)
                continue;
            var svg = LineSvg.GetDirectLineSvg(directLine, node, nodeCanvasPos, childrenZoom);
            if (svg.Length > 0)
                yield return svg;
        }
    }

    readonly record struct RenderContext(Pos CanvasOffset, double Zoom, Rect TileBounds, Pos TilePosition)
    {
        public RenderContext With(Pos canvasOffset, double zoom) => new(canvasOffset, zoom, TileBounds, TilePosition);

        public RenderContext ForNestedContainer(Rect tileRect) =>
            new(Pos.Zero, Zoom, TileBounds, new Pos(tileRect.X, tileRect.Y));
    }

    readonly record struct NodeGeometry(Rect CanvasRect, Rect TileRect);
}
