using Dependinator.Models;

namespace Dependinator.Diagrams.Svg;

interface ISvgService
{
    Tile GetTile(Rect viewRect, double zoom);
}

[Transient]
class SvgService : ISvgService
{
    readonly IModelService modelService;

    public SvgService(IModelService modelService)
    {
        this.modelService = modelService;
    }

    public Tile GetTile(Rect viewRect, double zoom)
    {
        //Log.Info("Get tile", zoom, viewRect.X, viewRect.Y);
        using var model = modelService.UseModel();

        if (model.Root.Children.Count == 0)
            return Tile.Empty;
        if (viewRect.Width == 0 || viewRect.Height == 0)
            return Tile.Empty;

        model.ViewRect = viewRect;
        model.Zoom = zoom;

        if (model.Tiles.TryGetLastUsed(viewRect, zoom, out var tile))
            return tile; // Same tile as last call

        var tileKey = TileKey.From(viewRect, zoom);
        if (model.Tiles.TryGetCached(tileKey, viewRect, zoom, out tile))
            return tile;
        // Log.Info("/n+++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++");
        // Log.Info($"Not Cached {tileKey}, for viewRect {viewRect} viewZoom: {zoom}, Tile:{tileKey.GetTileRect()}");

        // Create a new tile and cache it
        tile = CreateModelTile(model, tileKey);
        model.Tiles.SetCached(tile, viewRect, zoom);

        // Log.Info($"Tile: K:{tile.Key}, O: {tile.Offset}, Z: {tile.Zoom}, svg: {tile.Svg.Length} chars, Tiles: {model.Tiles}");
        return tile;
    }

    static Tile CreateModelTile(IModel model, TileKey tileKey)
    {
        // using var t = Timing.Start($"GetModelSvg: {tileKey}");
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
        var tileViewBox = $"{x} {y} {w} {h}";
        var tileSvg = $"""
            <svg width="{w}" height="{h}" viewBox="{tileViewBox}" xmlns="http://www.w3.org/2000/svg">
              {rootContentSvg}
            </svg>
            """;

        return new Tile(tileKey, tileSvg, tileZoom, tileOffset);
    }

    static string RenderNodeContent(Node node, RenderContext context)
    {
        // Log.Info($"RenderNodeContent '{node.Name}', context: {context}");
        EnsureChildrenLayout(node);

        var childrenCanvasOffset = CalculateChildrenCanvasOffset(node, context);
        var childrenZoom = context.Zoom * node.ContainerZoom;
        var childrenContext = context.With(childrenCanvasOffset, childrenZoom);

        var childNodeSvg = node
            .Children.Select(child => RenderNode(child, childrenContext))
            .Where(svg => svg.Length > 0);

        return childNodeSvg.Concat(RenderNodeLines(node, childrenCanvasOffset, context.Zoom, childrenZoom)).Join("\n");
    }

    static string RenderNode(Node node, RenderContext context)
    {
        var geometry = CalculateNodeGeometry(node, context);

        if (!RectsOverlap(context.TileBounds, geometry.TileRect))
            return "";

        if (node.Type == Parsing.NodeType.Member)
            return NodeSvg.GetMemberNodeSvg(node, geometry.CanvasRect, context.Zoom);

        if (NodeSvg.IsShowIcon(node.Type, context.Zoom))
            return NodeSvg.GetNodeIconSvg(node, geometry.CanvasRect, context.Zoom);

        var nestedContext = context.ForNestedContainer(geometry.TileRect);
        var childrenContentSvg = RenderNodeContent(node, nestedContext);

        if (NodeSvg.IsToLargeToBeSeen(context.Zoom))
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

    static bool RectsOverlap(Rect first, Rect second)
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
            yield return LineSvg.GetLineSvg(line, nodeCanvasPos, parentZoom, childrenZoom);
        }

        // All sibling lines and children to parent lines
        foreach (var child in node.Children)
        {
            foreach (var line in child.SourceLines)
            {
                if (line.Target.Parent == line.Source)
                    continue;
                yield return LineSvg.GetLineSvg(line, nodeCanvasPos, parentZoom, childrenZoom);
            }
        }

        foreach (var directLine in node.DirectLines)
        {
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
