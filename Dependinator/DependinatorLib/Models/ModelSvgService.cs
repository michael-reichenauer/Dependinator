namespace Dependinator.Models;


interface IModelSvgService
{
    Tile GetTile(IModel model, Rect viewRect, double zoom);
}


[Transient]
class ModelSvgService : IModelSvgService
{
    const int SmallIconSize = 9;
    const int FontSize = 8;

    const double MinContainerZoom = 1.0;
    const double MaxNodeZoom = 5 * 1 / Node.DefaultContainerZoom;           // To large to be seen


    public Tile GetTile(IModel model, Rect viewRect, double zoom)
    {
        // Log.Info($"GetSvg: {viewRect} zoom: {zoom}");
        if (!model.Root.Children.Any()) return Tile.Empty;

        var tileKey = TileKey.From(viewRect, zoom);
        if (model.Tiles.TryGetCached(tileKey, out var tile)) return tile;

        tile = GetModelTile(model, tileKey);
        model.Tiles.SetCached(tile);

        Log.Info("+++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++");
        Log.Info($"Tile: K:{tile.Key}, O: {tile.Offset}, Z: {tile.Zoom}, svg: {tile.Svg.Length} chars, Tiles: {model.Tiles}");
        return tile;
    }

    Tile GetModelTile(IModel model, TileKey tileKey)
    {
        // using var t = Timing.Start($"GetModelSvg: {tileKey}");
        var tileRect = tileKey.GetTileRect();
        var tileWithMargin = tileKey.GetTileRectWithMargin();
        var tileZoom = tileKey.GetTileZoom();

        var tileOffset = new Pos(-tileRect.X, -tileRect.Y);
        var svgContent = GetNodeContentSvg(model.Root, tileOffset, 1 / tileZoom, tileWithMargin);

        var (x, y, w, h) = tileKey.GetViewRect();

        var tileViewBox = $"{x} {y} {w} {h}";
        var tileSvg = $"""<svg width="{w}" height="{h}" viewBox="{tileViewBox}" xmlns="http://www.w3.org/2000/svg">{svgContent}</svg>""";

        return new Tile(tileKey, tileSvg, tileZoom, tileOffset);
    }


    static string GetNodeSvg(Node node, Pos offset, double zoom, Rect tileWithMargin)
    {
        var nodeCanvasPos = GetNodeCanvasPos(node, offset, zoom);
        var nodeCanvasRect = GetNodeCanvasRect(node, nodeCanvasPos, zoom);

        if (!IsOverlap(tileWithMargin, nodeCanvasRect)) return ""; // Outside the tile limit

        if (IsToLargeToBeSeen(zoom)) return GetNodeContentSvg(node, nodeCanvasPos, zoom, tileWithMargin);

        if (IsShowIcon(node, zoom)) return GetNodeIconSvg(node, nodeCanvasRect, zoom);

        return
            GetNodeContainerSvg(node, nodeCanvasRect, zoom) + "\n" +
            GetNodeContentSvg(node, nodeCanvasPos, zoom, tileWithMargin);
    }


    static string GetNodeContentSvg(Node node, Pos nodeCanvasPos, double zoom, Rect tileWithMargin)
    {
        var childrenZoom = zoom * node.ContainerZoom;

        return node.Children
            .Select(n => GetNodeSvg(n, nodeCanvasPos, childrenZoom, tileWithMargin))
            .Concat(GetNodeLinesSvg(node, nodeCanvasPos, childrenZoom))
            .Join("\n");
    }


    static IEnumerable<string> GetNodeLinesSvg(Node node, Pos nodeCanvasPos, double childrenZoom)
    {
        // !!! Must also add parent to children lines

        foreach (var child in node.Children)
        {
            foreach (var line in child.SourceLines)
            {
                yield return GetLineSvg(line, nodeCanvasPos, childrenZoom);
            }
        }
    }

    static bool IsToLargeToBeSeen(double zoom) => zoom > MaxNodeZoom;

    static bool IsShowIcon(Node node, double zoom) =>
        node.Type == NodeType.Member || zoom <= MinContainerZoom;

    static Pos GetNodeCanvasPos(Node node, Pos offset, double zoom) => new(
       offset.X + node.Boundary.X * zoom, offset.Y + node.Boundary.Y * zoom);

    static Rect GetNodeCanvasRect(Node node, Pos nodeCanvasPos, double zoom) => new(
       nodeCanvasPos.X, nodeCanvasPos.Y, node.Boundary.Width * zoom, node.Boundary.Height * zoom);


    static bool IsOverlap(Rect r1, Rect r2)
    {
        // Check if one rectangle is to the left or above the other
        if (r1.X + r1.Width < r2.X || r2.X + r2.Width < r1.X) return false;
        if (r1.Y + r1.Height < r2.Y || r2.Y + r2.Height < r1.Y) return false;

        return true;
    }

    static Rect GetIntersection(Rect rect1, Rect rect2)
    {
        double x1 = Math.Max(rect1.X, rect2.X);
        double y1 = Math.Max(rect1.Y, rect2.Y);
        double x2 = Math.Min(rect1.X + rect1.Width, rect2.X + rect2.Width);
        double y2 = Math.Min(rect1.Y + rect1.Height, rect2.Y + rect2.Height);

        // Check if there is a valid intersection
        if (x1 < x2 && y1 < y2)
        {
            return new Rect(x1, y1, x2 - x1, y2 - y1);
        }

        return Rect.None;
    }

    static Rect GetTotalBoundary(Node node)
    {
        (double x1, double y1, double x2, double y2) =
            (double.MaxValue, double.MaxValue, double.MinValue, double.MinValue);
        foreach (var child in node.Children)
        {
            var b = child.Boundary;
            x1 = Math.Min(x1, b.X);
            y1 = Math.Min(y1, b.Y);
            x2 = Math.Max(x2, b.X + b.Width);
            y2 = Math.Max(y2, b.Y + b.Height);
        }

        return new Rect(x1, y1, x2 - x1, y2 - y1);
    }


    static string GetNodeIconSvg(Node node, Rect parentCanvasRect, double parentZoom)
    {
        var (x, y) = (parentCanvasRect.X, parentCanvasRect.Y);
        var (w, h) = (node.Boundary.Width * parentZoom, node.Boundary.Height * parentZoom);

        var (tx, ty) = (x + w / 2, y + h);
        var fz = FontSize * parentZoom;
        var icon = node.Type.IconName;
        //Log.Info($"Icon: {node.LongName} ({x},{y},{w},{h}) ,{node.Boundary}, Z: {parentZoom}");
        //var toolTip = $"{node.HtmlLongName}, np: {node.Boundary}, Zoom: {parentZoom}, cr: {x}, {y}, {w}, {h}";

        var selectedSvg = node.IsSelected
            ? $"""<rect x="{x - 3}" y="{y - 3}" width="{w + 6}" height="{h + 6}" stroke-width="3" rx="2" fill="none" stroke="yellow" />"""
            : "";
        return
            $"""
            {selectedSvg}
            <use href="#{icon}" x="{x}" y="{y}" width="{w}" height="{h}" />
            <text x="{tx}" y="{ty}" class="iconName" font-size="{fz}px">{node.HtmlShortName}</text>
            <g class="hoverable">
              <rect id="{node.Id.Value}" x="{x - 2}" y="{y - 2}" width="{w + 2}" height="{h + 2}" stroke-width="1" rx="2" fill="black" fill-opacity="0" stroke="none">
              </rect>
            </g>
            </rect>
            """;
    }


    static string GetNodeContainerSvg(Node node, Rect parentCanvasRect, double parentZoom)
    {
        var s = node.StrokeWidth;
        var (x, y) = (parentCanvasRect.X, parentCanvasRect.Y);
        var (w, h) = (node.Boundary.Width * parentZoom, node.Boundary.Height * parentZoom);
        var (ix, iy, iw, ih) = (x, y + h + 1 * parentZoom, SmallIconSize * parentZoom, SmallIconSize * parentZoom);

        var (tx, ty) = (x + (SmallIconSize + 1) * parentZoom, y + h + 2 * parentZoom);
        var fz = FontSize * parentZoom;
        var icon = node.Type.IconName;

        //Log.Info($"Container: {node.LongName} ({x},{y},{w},{h}) ,{node.Boundary}, Z: {parentZoom}");
        //var toolTip = $"{node.HtmlLongName}, np: {node.Boundarsy}, Zoom: {parentZoom}, cr: {x}, {y}, {w}, {h}";
        var selectedSvg = node.IsSelected
            ? $"""<rect x="{x - 3}" y="{y - 3}" width="{w + 6}" height="{h + 6}" stroke-width="3" rx="2" fill="none" stroke="yellow" />"""
            : "";

        return
            $"""
            {selectedSvg}
            <rect x="{x}" y="{y}" width="{w}" height="{h}" stroke-width="{s}" rx="5" fill="{node.Background}" fill-opacity="1" stroke="{node.StrokeColor}"/>      
            <use href="#{icon}" x="{ix}" y="{iy}" width="{iw}" height="{ih}" />
            <text x="{tx}" y="{ty}" class="nodeName" font-size="{fz}px">{node.HtmlShortName}</text>
            <g class="hoverable">
              <rect id="{node.Id.Value}" x="{x - 2}" y="{y - 2}" width="{w + 2}" height="{h + 2}" stroke-width="1" rx="2" fill="black" fill-opacity="0" stroke="none">
              </rect>
            </g>
            """;
    }

    static string GetLineSvg(Line line, Pos nodeCanvasPos, double zoom)
    {
        if (IsToLargeToBeSeen(zoom)) return "";

        var (x1, y1, x2, y2) = line.GetLineEndpoints();

        // !!!!! Fel i koden, samma rad i if och else delen 
        if (line.Source != line.Target.Parent)
        {
            (x1, y1) = (nodeCanvasPos.X + x1 * zoom, nodeCanvasPos.Y + y1 * zoom);
        }
        else
        {
            (x1, y1) = (nodeCanvasPos.X + x1 * zoom, nodeCanvasPos.Y + y1 * zoom);
        }

        (x2, y2) = (nodeCanvasPos.X + x2 * zoom, nodeCanvasPos.Y + y2 * zoom);

        var s = line.StrokeWidth;

        return
            $"""
            <g class="hoverable" >
              <line x1="{x1}" y1="{y1}" x2="{x2}" y2="{y2}" stroke-width="{s}" stroke="white" marker-end="url(#arrow)" />
            </g>
            """;
    }
}
