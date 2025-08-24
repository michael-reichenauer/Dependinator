using Dependinator.Models;

namespace Dependinator.Diagrams.Svg;

class NodeSvg
{
    const double MaxNodeZoom = 8 * 1 / Node.DefaultContainerZoom; // To large to be seen
    const double MinContainerZoom = 2.0;
    const int SmallIconSize = 9;
    const int FontSize = 8;

    public static bool IsToLargeToBeSeen(double zoom) => zoom > MaxNodeZoom;

    public static bool IsShowIcon(NodeType nodeType, double zoom) =>
        nodeType == NodeType.Member || zoom <= MinContainerZoom;

    public static string GetNodeIconSvg(Node node, Rect nodeCanvasRect, double parentZoom)
    {
        //Log.Info("Draw", node.Name, nodeCanvasRect.ToString());
        var s = node.IsEditMode ? 10 : node.StrokeWidth;
        var (x, y) = (nodeCanvasRect.X, nodeCanvasRect.Y);
        var (w, h) = (node.Boundary.Width * parentZoom, node.Boundary.Height * parentZoom);

        var (tx, ty) = (x + w / 2, y + h);
        var fz = FontSize * parentZoom;
        var icon = node.Type.IconName;
        //Log.Info($"Icon: {node.LongName} ({x},{y},{w},{h}) ,{node.Boundary}, Z: {parentZoom}");
        //var toolTip = $"{node.HtmlLongName}, np: {node.Boundary}, Zoom: {parentZoom}, cr: {x}, {y}, {w}, {h}";
        string selectedSvg = SelectedNodeSvg(node, x, y, w, h);
        var elementId = PointerId.FromNode(node.Id).ElementId;

        var hiddenNode = node.IsHidden ? "opacity=\"0.1\"" : "";
        var hiddenText = node.IsHidden ? "opacity=\"0.3\"" : "";
        return $"""
            <use href="#{icon}" x="{x:0.##}" y="{y:0.##}" width="{w:0.##}" height="{h:0.##}" {hiddenNode} />
            <text x="{tx:0.##}" y="{ty:0.##}" class="iconName" font-size="{fz:0.##}px" {hiddenText} >{node.HtmlShortName}</text>
            <g class="hoverable" id="{elementId}">
              <rect id="{elementId}" x="{x:0.##}" y="{y:0.##}" width="{w:0.##}" height="{h:0.##}" stroke-width="1" rx="2" fill="black" fill-opacity="0" stroke="none"/>
              <title>{node.HtmlLongName}</title>
            </g>
            {selectedSvg}
            """;
    }

    public static string GetNodeContainerSvg(Node node, Rect nodeCanvasRect, double parentZoom, string childrenContent)
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
        var (border, background) = DColors.NodeColorByName(node.Color);
        var c = node.IsEditMode ? DColors.EditNodeBorder : border;
        var back = node.IsEditMode ? DColors.EditNodeBackground : background;
        var hiddenNode = node.IsHidden ? "opacity=\"0.1\"" : "";
        var hiddenText = node.IsHidden ? "opacity=\"0.3\"" : "";

        return $"""
            <svg x="{x:0.##}" y="{y:0.##}" width="{w:0.##}" height="{h:0.##}" viewBox="{0} {0} {w:0.##} {h:0.##}" xmlns="http://www.w3.org/2000/svg">
              <rect x="{0}" y="{0}" width="{w:0.##}" height="{h:0.##}" stroke-width="{s}" rx="5" fill="{back}" stroke="{c}" {hiddenNode}/>
              <g class="{cl}" id="{elementId}">
                <rect id="{elementId}" x="0" y="0" width="{w:0.##}" height="{h:0.##}" stroke-width="1" rx="2" fill="black" fill-opacity="0" stroke="none"/>
                <title>{node.HtmlLongName}</title>
              </g>
              {childrenContent}          
            </svg>
            <use href="#{icon}" x="{ix:0.##}" y="{iy:0.##}" width="{iw:0.##}" height="{ih:0.##}" {hiddenText}/>
            <text x="{tx:0.##}" y="{ty:0.##}" class="nodeName" font-size="{fz:0.##}px" {hiddenText}>{node.HtmlShortName}</text>
            {selectedSvg}
            """;
    }

    public static string GetToLargeNodeContainerSvg(Rect nodeCanvasRect, string childrenContent)
    {
        //Log.Info("Draw", node.Name, nodeCanvasRect.ToString());
        var (x, y, w, h) = (nodeCanvasRect.X, nodeCanvasRect.Y, nodeCanvasRect.Width, nodeCanvasRect.Height);
        return $"""
              <svg x="{x:0.##}" y="{y:0.##}" width="{w:0.##}" height="{h:0.##}" viewBox="0 0 {w:0.##} {h:0.##}" xmlns="http://www.w3.org/2000/svg">
                {childrenContent}
              </svg>
            """;
    }

    static string SelectedNodeSvg(Node node, double x, double y, double w, double h)
    {
        if (!node.IsSelected)
            return "";

        string c = DColors.Selected;
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
}
