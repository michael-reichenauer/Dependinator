using Dependinator.Diagrams;

namespace Dependinator.Models;

interface ISvgService
{
    Tile GetTile(IModel model, Rect viewRect, double zoom);
}

[Transient]
class SvgService : ISvgService
{
    const int SmallIconSize = 9;
    const int FontSize = 8;

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

    static string GetNodeContentSvg(Node node, Pos nodeCanvasPos, double zoom, Rect tileWithMargin, Pos nodeRealPos)
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

        if (node.IsShowIcon(zoom))
            return GetNodeIconSvg(node, nodeSvgRect, zoom);

        var nodeContentContentSvg = GetNodeContentSvg(node, Pos.Zero, zoom, tileWithMargin, nodeTilePos);

        if (Node.IsToLargeToBeSeen(zoom))
            return GetToLargeNodeContainerSvg(nodeSvgRect, nodeContentContentSvg);

        return GetNodeContainerSvg(node, nodeSvgRect, zoom, nodeContentContentSvg);
    }

    static IEnumerable<string> GetNodeLinesSvg(Node node, Pos nodeCanvasPos, double parentZoom, double childrenZoom)
    {
        var parentToChildrenLines = node.SourceLines.Where(l => l.Target.Parent == node);
        foreach (var line in parentToChildrenLines)
        {
            yield return GetLineSvg(line, nodeCanvasPos, parentZoom, childrenZoom);
        }

        // All sibling lines and children to parent lines
        foreach (var child in node.Children)
        {
            foreach (var line in child.SourceLines)
            {
                if (line.Target.Parent == line.Source)
                    continue;
                yield return GetLineSvg(line, nodeCanvasPos, parentZoom, childrenZoom);
            }
        }
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

    static string GetNodeIconSvg(Node node, Rect nodeCanvasRect, double parentZoom)
    {
        //Log.Info("Draw", node.Name, nodeCanvasRect.ToString());
        var (x, y) = (nodeCanvasRect.X, nodeCanvasRect.Y);
        var (w, h) = (node.Boundary.Width * parentZoom, node.Boundary.Height * parentZoom);

        var (tx, ty) = (x + w / 2, y + h);
        var fz = FontSize * parentZoom;
        var icon = node.Type.IconName;
        //Log.Info($"Icon: {node.LongName} ({x},{y},{w},{h}) ,{node.Boundary}, Z: {parentZoom}");
        //var toolTip = $"{node.HtmlLongName}, np: {node.Boundary}, Zoom: {parentZoom}, cr: {x}, {y}, {w}, {h}";
        string selectedSvg = SelectedNodeSvg(node, x, y, w, h);
        var elementId = PointerId.FromNode(node.Id).ElementId;

        return $"""
            <use href="#{icon}" x="{x:0.##}" y="{y:0.##}" width="{w:0.##}" height="{h:0.##}" />
            <text x="{tx:0.##}" y="{ty:0.##}" class="iconName" font-size="{fz:0.##}px">{node.HtmlShortName}</text>
            <g class="hoverable" id="{elementId}">
              <rect id="{elementId}" x="{x:0.##}" y="{y:0.##}" width="{w:0.##}" height="{h:0.##}" stroke-width="1" rx="2" fill="black" fill-opacity="0" stroke="none"/>
              <title>{node.HtmlLongName}</title>
            </g>
            {selectedSvg}
            """;
    }

    static string GetNodeContainerSvg(Node node, Rect nodeCanvasRect, double parentZoom, string childrenContent)
    {
        //Log.Info("Draw", node.Name, nodeCanvasRect.ToString());
        var s = node.IsEditMode ? 10 : node.StrokeWidth;
        var (x, y, w, h) = (nodeCanvasRect.X, nodeCanvasRect.Y, nodeCanvasRect.Width, nodeCanvasRect.Height);

        var iSize = SmallIconSize * parentZoom;
        var (ix, iy, iw, ih) = (x, y + h + 1 * parentZoom, iSize, iSize);

        var (tx, ty) = (x + (SmallIconSize + 1) * parentZoom, y + h + 2 * parentZoom);
        var fz = FontSize * parentZoom;
        var icon = node.Type.IconName;
        var elementId = PointerId.FromNode(node.Id).ElementId;
        //Log.Info($"Container: {node.LongName} ({x},{y},{w},{h}) ,{node.Boundary}, Z: {parentZoom}");
        //var toolTip = $"{node.HtmlLongName}, np: {node.Boundary}, Zoom: {parentZoom}, cr: {x}, {y}, {w}, {h}";

        string selectedSvg = SelectedNodeSvg(node, x, y, w, h);

        var cl = node.IsEditMode ? "hoverableedit" : "hoverable";
        var c = node.IsEditMode ? Coloring.EditNode : node.Color;
        var back = node.IsEditMode ? Coloring.EditNodeBack : node.Background;

        return $"""
            <svg x="{x:0.##}" y="{y:0.##}" width="{w:0.##}" height="{h:0.##}" viewBox="{0} {0} {w:0.##} {h:0.##}" xmlns="http://www.w3.org/2000/svg">
              <rect x="{0}" y="{0}" width="{w:0.##}" height="{h:0.##}" stroke-width="{s}" rx="5" fill="{back}" stroke="{c}"/>
              <g class="{cl}" id="{elementId}">
                <rect id="{elementId}" x="{0}" y="{0}" width="{w:0.##}" height="{h:0.##}" stroke-width="1" rx="2" fill="black" fill-opacity="0" stroke="none"/>
                <title>{node.HtmlLongName}</title>
              </g>
              {childrenContent}
            </svg>
            <use href="#{icon}" x="{ix:0.##}" y="{iy:0.##}" width="{iw:0.##}" height="{ih:0.##}" />
            <text x="{tx:0.##}" y="{ty:0.##}" class="nodeName" font-size="{fz:0.##}px">{node.HtmlShortName}</text>
            {selectedSvg}
            """;
    }

    static string GetToLargeNodeContainerSvg(Rect nodeCanvasRect, string childrenContent)
    {
        //Log.Info("Draw", node.Name, nodeCanvasRect.ToString());
        var (x, y, w, h) = (nodeCanvasRect.X, nodeCanvasRect.Y, nodeCanvasRect.Width, nodeCanvasRect.Height);
        return $"""
              <svg x="{x:0.##}" y="{y:0.##}" width="{w:0.##}" height="{h:0.##}" viewBox="{0} {0} {w:0.##} {h:0.##}" xmlns="http://www.w3.org/2000/svg">
                {childrenContent}
              </svg>
            """;
    }

    static string SelectedNodeSvg(Node node, double x, double y, double w, double h)
    {
        if (!node.IsSelected)
            return "";

        string c = Coloring.Highlight;
        const int s = 8;
        const int m = 3;
        const int mt = m + s;
        const int ml = m + s;
        const int mm = s / 2;
        const int mr = m;
        const int mb = m;
        const int rp = 6;
        const int rs = 13;

        const int tt = 12;
        const int t = 10 * 3 + 1;
        var etl = PointerId.FromNodeResize(node.Id, NodeResizeType.TopLeft).ElementId;
        var etm = PointerId.FromNodeResize(node.Id, NodeResizeType.TopMiddle).ElementId;
        var etr = PointerId.FromNodeResize(node.Id, NodeResizeType.TopRight).ElementId;
        var eml = PointerId.FromNodeResize(node.Id, NodeResizeType.MiddleLeft).ElementId;
        var emr = PointerId.FromNodeResize(node.Id, NodeResizeType.MiddleRight).ElementId;
        var ebl = PointerId.FromNodeResize(node.Id, NodeResizeType.BottomLeft).ElementId;
        var ebm = PointerId.FromNodeResize(node.Id, NodeResizeType.BottomMiddle).ElementId;
        var ebr = PointerId.FromNodeResize(node.Id, NodeResizeType.BottomRight).ElementId;

        return $"""
            <rect x="{x-rp}" y="{y-rp}" width="{w + rs:0.##}" height="{h
                + rs:0.##}" stroke-width="0.5" rx="0" fill="none" stroke="{c}" stroke-dasharray="5,5"/>

            <g class="selectpoint">
                <circle id="{etl}" cx="{x - ml + s/2.0}" cy="{y - mt + s/2.0}" r="{s/2.0}" fill="{c}" />
                <circle id="{etl}" cx="{x - ml - tt + t/2.0}"  cy="{y - mt - tt + t/2.0}"  r="{t/2.0}" fill="{c}" fill-opacity="0"/>
            </g>
            <g class="selectpoint">
                <circle id="{etm}" cx="{x + w/2 - mm + s/2.0}" cy="{y - mt + s/2.0}" r="{s/2.0}" fill="{c}" />
                <circle id="{etm}" cx="{x + w/2 - mm - tt + t/2.0}" cy="{y - mt - tt + t/2.0}" r="{t/2.0}" fill="{c}" fill-opacity="0"/>
            </g>
            <g class="selectpoint">
                <circle id="{etr}" cx="{x + w + mr + s/2.0}" cy="{y - mt + s/2.0}" r="{s/2.0}" fill="{c}" />
                <circle id="{etr}" cx="{x + w + mr - tt + t/2.0}" cy="{y - mt - tt + t/2.0}"  r="{t/2.0}" fill="{c}" fill-opacity="0"/>
            </g>
            <g class="selectpoint">
                <circle id="{eml}" cx="{x - ml + s/2.0}" cy="{y + h/2 + s/2.0}" r="{s/2.0}" fill="{c}" />
                <circle id="{eml}" cx="{x - ml - tt + t/2.0}"  cy="{y + h/2 - tt + t/2.0}" r="{t/2.0}" fill="{c}" fill-opacity="0"/>
            </g>
            <g class="selectpoint">
                <circle id="{emr}" cx="{x + w + mr + s/2.0}" cy="{y + h/2 + s/2.0}" r="{s/2.0}" fill="{c}" />
                <circle id="{emr}" cx="{x + w + mr - tt + t/2.0}" cy="{y + h/2 - tt + t/2.0}" r="{t/2.0}" fill="{c}" fill-opacity="0"/>
            </g>
            <g class="selectpoint">
                <circle id="{ebl}" cx="{x - ml + s/2.0}" cy="{y + h + mb + s/2.0}" r="{s/2.0}" fill="{c}" />
                <circle id="{ebl}" cx="{x - ml - tt + t/2.0}"  cy="{y + h + mb - tt + t/2.0}" r="{t/2.0}" fill="{c}" fill-opacity="0"/>
            </g>
            <g class="selectpoint">
                <circle id="{ebm}" cx="{x + w/2 - mm + s/2.0}" cy="{y + h + mb + s/2.0}" r="{s/2.0}" fill="{c}" />
                <circle id="{ebm}" cx="{x + w/2 - mm - tt + t/2.0}" cy="{y + h + mb - tt + t/2.0}" r="{t/2.0}" fill="{c}" fill-opacity="0"/>
            </g>
            <g class="selectpoint">
                <circle id="{ebr}" cx="{x + w + mr + s/2.0}" cy="{y + h + mb + s/2.0}" r="{s/2.0}" fill="{c}" />
                <circle id="{ebr}" cx="{x + w + mr - tt + t/2.0}" cy="{y + h + mb - tt + t/2.0}" r="{t/2.0}" fill="{c}" fill-opacity="0"/>
            </g>
            """;
    }

    static string GetLineSvg(Line line, Pos nodeCanvasPos, double parentZoom, double childrenZoom)
    {
        if (Node.IsToLargeToBeSeen(childrenZoom))
            return "";

        var sw = line.StrokeWidth;
        var (s, t) = (line.Source.Boundary, line.Target.Boundary);

        var (x1, y1) = (s.X + s.Width, s.Y + s.Height / 2);
        var (x2, y2) = (t.X, t.Y + t.Height / 2);

        if (line.Target.Parent == line.Source)
        { // Parent source to child target (left of parent to right of child)
            if (Node.IsToLargeToBeSeen(parentZoom))
                return "";
            var parent = line.Source;
            (x1, y1) = (
                nodeCanvasPos.X - parent.ContainerOffset.X * parentZoom,
                nodeCanvasPos.Y + s.Height / 2 * parentZoom - parent.ContainerOffset.Y * parentZoom
            );
            (x2, y2) = (nodeCanvasPos.X + x2 * childrenZoom, nodeCanvasPos.Y + y2 * childrenZoom);
        }
        else if (line.Source.Parent == line.Target)
        { // Child source to parent target (left of child to right of parent)
            if (Node.IsToLargeToBeSeen(parentZoom))
                return "";
            var parent = line.Target;
            (x1, y1) = (nodeCanvasPos.X + x1 * childrenZoom, nodeCanvasPos.Y + y1 * childrenZoom);

            (x2, y2) = (
                nodeCanvasPos.X + t.Width * parentZoom - parent.ContainerOffset.X * parentZoom,
                nodeCanvasPos.Y + t.Height / 2 * parentZoom - parent.ContainerOffset.Y * parentZoom
            );
        }
        else
        { // Sibling source to sibling target (right of source to left of target)
            (x1, y1) = (nodeCanvasPos.X + x1 * childrenZoom, nodeCanvasPos.Y + y1 * childrenZoom);
            (x2, y2) = (nodeCanvasPos.X + x2 * childrenZoom, nodeCanvasPos.Y + y2 * childrenZoom);
        }
        var elementId = PointerId.FromLine(line.Id).ElementId;
        string selectedSvg = SelectedLineSvg(line, x1, y1, x2, y2);

        var c = "#B388FF";
        return $"""
            <line x1="{x1}" y1="{y1}" x2="{x2}" y2="{y2}" stroke-width="{sw}" stroke="{c}" marker-end="url(#arrow)" />
            <g class="hoverable" id="{elementId}">
              <line id="{elementId}" x1="{x1}" y1="{y1}" x2="{x2}" y2="{y2}" stroke-width="{sw
                + 10}" stroke="black" stroke-opacity="0" />
              <title>{line.Source.HtmlLongName}→{line.Target.HtmlLongName}</title>
            </g>
            {selectedSvg}
            """;
    }

    private static string SelectedLineSvg(Line line, double x1, double y1, double x2, double y2)
    {
        if (!line.IsSelected)
            return "";

        string c = Coloring.Highlight;
        var sw = line.StrokeWidth;
        var ps = 7;

        return $"""
            <line x1="{x1}" y1="{y1}" x2="{x2}" y2="{y2}" stroke="{c}" stroke-width="{sw
                + 3}" stroke-dasharray="3,50"/>
            <circle cx="{x1}" cy="{y1}" r="{ps}" fill="{c}" />
            <circle cx="{x2}" cy="{y2}" r="{ps}" fill="{c}" />
            """;
    }
}
