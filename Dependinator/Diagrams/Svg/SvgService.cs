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

        if (model.Root.Children.Any())
        {
            model.ViewRect = viewRect;
            model.Zoom = zoom;
        }
        return GetTile(model, viewRect, zoom);
    }

    public Tile GetTile(IModel model, Rect viewRect, double zoom)
    {
        // Log.Info($"GetSvg: {viewRect} zoom: {zoom}");
        if (model.Root.Children.Count == 0)
            return Tile.Empty;
        if (viewRect.Width == 0 || viewRect.Height == 0)
            return Tile.Empty;

        if (model.Tiles.TryGetLastUsed(viewRect, zoom, out var tile))
            return tile; // Same tile as last call

        var tileKey = TileKey.From(viewRect, zoom);
        if (model.Tiles.TryGetCached(tileKey, viewRect, zoom, out tile))
            return tile;
        // Log.Info("/n+++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++");
        // Log.Info($"Not Cached {tileKey}, for viewRect {viewRect} viewZoom: {zoom}, Tile:{tileKey.GetTileRect()}");

        tile = GetModelTile(model, tileKey);
        model.Tiles.SetCached(tile, viewRect, zoom);

        // Log.Info($"Tile: K:{tile.Key}, O: {tile.Offset}, Z: {tile.Zoom}, svg: {tile.Svg.Length} chars, Tiles: {model.Tiles}");
        return tile;
    }

    static Tile GetModelTile(IModel model, TileKey tileKey)
    {
        // using var t = Timing.Start($"GetModelSvg: {tileKey}");
        var tileRect = tileKey.GetTileRect();
        var tileWithMargin = tileKey.GetTileRectWithMargin();
        var tileZoom = tileKey.GetTileZoom();
        var tileOffset = new Pos(-tileRect.X, -tileRect.Y);

        var rootContentSvg = GetNodeContentSvg(model.Root, tileOffset, 1 / tileZoom, tileWithMargin, new Pos(0, 0));

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

    public static string GetNodeContentSvg(
        Node node,
        Pos nodeCanvasPos,
        double zoom,
        Rect tileWithMargin,
        Pos nodeRealPos
    )
    {
        // Log.Info($"GetNodeContentSvg '{node.Name}', nodeRealPos: {nodeRealPos}, Z:{zoom}");
        if (node.IsChildrenLayoutRequired)
            NodeLayout.AdjustChildren(node);

        var childrenPos = new Pos(
            nodeCanvasPos.X + node.ContainerOffset.X * zoom,
            nodeCanvasPos.Y + node.ContainerOffset.Y * zoom
        );
        var childrenZoom = zoom * node.ContainerZoom;

        return node
            .Children.Select(n => GetNodeSvg(n, childrenPos, childrenZoom, tileWithMargin, nodeRealPos))
            .Concat(GetNodeLinesSvg(node, childrenPos, zoom, childrenZoom))
            .Join("\n");
    }

    static string GetNodeSvg(Node node, Pos offset, double zoom, Rect tileWithMargin, Pos parentTilePos)
    {
        // Log.Info("---------------------------------------------");
        // Log.Info($"GetNodeSvg '{node.Name}', offset: {offset}, Z:{zoom}, ParentRealPos: {parentRealPos}");
        var nodeSvgPos = GetNodeSvgPos(node, offset, zoom);
        var nodeSvgRect = GetNodeSvgRect(node, nodeSvgPos, zoom);
        var nodeTilePos = GetNodeTilePos(node, parentTilePos, offset, zoom);

        if (!IsInsideTile(node, nodeTilePos, zoom, tileWithMargin))
            return "";

        if (NodeSvg.IsShowIcon(node.Type, zoom))
            return NodeSvg.GetNodeIconSvg(node, nodeSvgRect, zoom);

        var nodeContentContentSvg = GetNodeContentSvg(node, Pos.Zero, zoom, tileWithMargin, nodeTilePos);

        if (NodeSvg.IsToLargeToBeSeen(zoom))
            return NodeSvg.GetToLargeNodeContainerSvg(nodeSvgRect, nodeContentContentSvg);

        return NodeSvg.GetNodeContainerSvg(node, nodeSvgRect, zoom, nodeContentContentSvg);
    }

    static Pos GetNodeSvgPos(Node node, Pos offset, double zoom) =>
        new(offset.X + node.Boundary.X * zoom, offset.Y + node.Boundary.Y * zoom);

    static Rect GetNodeSvgRect(Node node, Pos nodeCanvasPos, double zoom) =>
        new(nodeCanvasPos.X, nodeCanvasPos.Y, node.Boundary.Width * zoom, node.Boundary.Height * zoom);

    static Pos GetNodeTilePos(Node node, Pos parentTilePos, Pos offset, double zoom) =>
        new(parentTilePos.X + node.Boundary.X * zoom + offset.X, parentTilePos.Y + node.Boundary.Y * zoom + offset.Y);

    static bool IsInsideTile(Node node, Pos nodeRealPos, double zoom, Rect tileWithMargin)
    {
        var nodeTileRect = new Rect(
            nodeRealPos.X,
            nodeRealPos.Y,
            node.Boundary.Width * zoom,
            node.Boundary.Height * zoom
        );
        return IsOverlap(tileWithMargin, nodeTileRect);
    }

    static bool IsOverlap(Rect r1, Rect r2)
    {
        // Check if one rectangle is to the left of the other
        if (r1.X + r1.Width <= r2.X || r2.X + r2.Width <= r1.X)
            return false;
        // Check if one rectangle is above the other
        if (r1.Y + r1.Height <= r2.Y || r2.Y + r2.Height <= r1.Y)
            return false;

        return true;
    }

    static IEnumerable<string> GetNodeLinesSvg(Node node, Pos nodeCanvasPos, double parentZoom, double childrenZoom)
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
    }
}
