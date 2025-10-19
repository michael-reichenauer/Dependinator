using Dependinator.Models;

namespace Dependinator.Diagrams.Svg;

class NodeSvg
{
    const double MaxNodeZoom = 8 * 1 / Node.DefaultContainerZoom; // To large to be seen
    const double MinContainerZoom = 2.0;
    const int NameIconSize = 9;
    const int FontSize = 8;

    public static bool IsToLargeToBeSeen(double zoom) => zoom > MaxNodeZoom;

    public static bool IsShowIcon(Parsing.NodeType nodeType, double zoom) =>
        nodeType == Parsing.NodeType.Member || zoom <= MinContainerZoom;

    public static string GetNodeIconSvg(Node node, Rect nodeCanvasRect, double parentZoom) =>
        BuildSimpleNodeSvg(node, nodeCanvasRect, parentZoom);

    public static string GetNodeContainerSvg(Node node, Rect nodeCanvasRect, double parentZoom, string childrenContent)
    {
        var geometry = nodeCanvasRect;
        var header = CalculateContainerHeader(nodeCanvasRect, parentZoom);
        var elementId = PointerId.FromNode(node.Id).ElementId;
        var (border, background) = NodeColors(node);
        var (nodeOpacity, textOpacity) = HiddenAttributes(node);
        var iconId = IconName(node.Type);
        var strokeWidth = node.IsEditMode ? 10 : node.StrokeWidth;
        var hoverClass = node.IsEditMode ? "hoverableedit" : "hoverable";
        var selectedOverlay = SelectedNodeSvg(node, geometry);

        var innerGeometry = new Rect(0, 0, geometry.Width, geometry.Height);
        var hoverGroup = BuildHoverGroup(elementId, hoverClass, innerGeometry, node.HtmlLongName);

        return $"""
            <svg x="{geometry.X:0.##}" y="{geometry.Y:0.##}" width="{geometry.Width:0.##}" height="{geometry.Height:0.##}" viewBox="{0} {0} {geometry.Width:0.##} {geometry.Height:0.##}" xmlns="http://www.w3.org/2000/svg">
              <rect x="{0}" y="{0}" width="{geometry.Width:0.##}" height="{geometry.Height:0.##}" stroke-width="{strokeWidth}" rx="5" fill="{background}" fill-opacity="0.5" stroke="{border}" {nodeOpacity}/>
              {hoverGroup}
              {childrenContent}          
            </svg>
            <use href="#{iconId}" xlink:href="#{iconId}" x="{header.IconPos.X:0.##}" y="{header.IconPos.Y:0.##}" width="{header.IconSize:0.##}" height="{header.IconSize:0.##}" {textOpacity}/>
            <text x="{header.TextPos.X:0.##}" y="{header.TextPos.Y:0.##}" class="nodeName" font-size="{header.FontSize:0.##}px" {textOpacity}>{node.HtmlShortName}</text>
            {selectedOverlay}
            """;
    }

    public static string GetMemberNodeSvg(Node node, Rect nodeCanvasRect, double parentZoom) =>
        BuildSimpleNodeSvg(node, nodeCanvasRect, parentZoom);

    public static string GetToLargeNodeContainerSvg(Rect nodeCanvasRect, string childrenContent)
    {
        var (x, y, w, h) = (nodeCanvasRect.X, nodeCanvasRect.Y, nodeCanvasRect.Width, nodeCanvasRect.Height);
        return $"""
              <svg x="{x:0.##}" y="{y:0.##}" width="{w:0.##}" height="{h:0.##}" viewBox="0 0 {w:0.##} {h:0.##}" xmlns="http://www.w3.org/2000/svg">
                {childrenContent}
              </svg>
            """;
    }

    static string BuildSimpleNodeSvg(Node node, Rect nodeCanvasRect, double parentZoom)
    {
        var geometry = CalculateIconGeometry(node, nodeCanvasRect, parentZoom);
        var textX = geometry.X + geometry.Width / 2;
        var textY = geometry.Y + geometry.Height;
        var fontSize = FontSize * parentZoom;
        var iconId = IconName(node.Type);
        var elementId = PointerId.FromNode(node.Id).ElementId;
        var (nodeOpacity, textOpacity) = HiddenAttributes(node);
        var hoverGroup = BuildHoverGroup(elementId, "hoverable", geometry, node.HtmlLongName);
        var selectedOverlay = SelectedNodeSvg(node, geometry);

        return $"""
            <use href="#{iconId}" xlink:href="#{iconId}" x="{geometry.X:0.##}" y="{geometry.Y:0.##}" width="{geometry.Width:0.##}" height="{geometry.Height:0.##}" {nodeOpacity} />
            <text x="{textX:0.##}" y="{textY:0.##}" class="iconName" font-size="{fontSize:0.##}px" {textOpacity} >{node.HtmlShortName}</text>
            {hoverGroup}
            {selectedOverlay}
            """;
    }

    static Rect CalculateIconGeometry(Node node, Rect nodeCanvasRect, double parentZoom)
    {
        var width = node.Boundary.Width * parentZoom;
        var height = node.Boundary.Height * parentZoom;
        return new Rect(nodeCanvasRect.X, nodeCanvasRect.Y, width, height);
    }

    static ContainerHeader CalculateContainerHeader(Rect nodeCanvasRect, double parentZoom)
    {
        var iconSize = NameIconSize * parentZoom;
        var iconPosition = new Pos(nodeCanvasRect.X, nodeCanvasRect.Y + nodeCanvasRect.Height + 1 * parentZoom);
        var textPosition = new Pos(
            nodeCanvasRect.X + (NameIconSize + 1) * parentZoom,
            nodeCanvasRect.Y + nodeCanvasRect.Height + 2 * parentZoom
        );
        var fontSize = FontSize * parentZoom;
        return new ContainerHeader(iconPosition, iconSize, textPosition, fontSize);
    }

    static (string NodeOpacity, string TextOpacity) HiddenAttributes(Node node) =>
        node.IsHidden ? ("opacity=\"0.1\"", "opacity=\"0.3\"") : ("", "");

    static (string Border, string Background) NodeColors(Node node)
    {
        var (border, background) = DColors.NodeColorByName(node.Color);
        if (node.IsEditMode)
            return (DColors.EditNodeBorder, DColors.EditNodeBackground);
        return (border, background);
    }

    static string BuildHoverGroup(string elementId, string cssClass, Rect geometry, string title) =>
        $"""
            <g class="{cssClass}" id="{elementId}">
              <rect id="{elementId}" x="{geometry.X:0.##}" y="{geometry.Y:0.##}" width="{geometry.Width:0.##}" height="{geometry.Height:0.##}" stroke-width="1" rx="2" fill="black" fill-opacity="0" stroke="none"/>
              <title>{title}</title>
            </g>
            """.Trim();

    static string SelectedNodeSvg(Node node, Rect geometry)
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

        var x = geometry.X;
        var y = geometry.Y;
        var w = geometry.Width;
        var h = geometry.Height;

        return $"""
            <rect x="{x - rp}" y="{y - rp}" width="{w + rs:0.##}" height="{h
                + rs:0.##}" stroke-width="0.5" rx="0" fill="none" stroke="{c}" stroke-dasharray="5,5"/>

            <g class="selectpoint">
                <circle id="{etl}" cx="{x - ml + s / 2.0}" cy="{y - mt + s / 2.0}" r="{s / 2.0}" fill="{c}" />
                <circle id="{etl}" cx="{x - ml - tt + t / 2.0}"  cy="{y - mt - tt + t / 2.0}"  r="{t / 2.0}" fill="{c}" fill-opacity="0"/>
            </g>
            <g class="selectpoint">
                <circle id="{etm}" cx="{x + w / 2 - mm + s / 2.0}" cy="{y - mt + s / 2.0}" r="{s / 2.0}" fill="{c}" />
                <circle id="{etm}" cx="{x + w / 2 - mm - tt + t / 2.0}" cy="{y - mt - tt + t / 2.0}" r="{t / 2.0}" fill="{c}" fill-opacity="0"/>
            </g>
            <g class="selectpoint">
                <circle id="{etr}" cx="{x + w + mr + s / 2.0}" cy="{y - mt + s / 2.0}" r="{s / 2.0}" fill="{c}" />
                <circle id="{etr}" cx="{x + w + mr - tt + t / 2.0}" cy="{y - mt - tt + t / 2.0}"  r="{t / 2.0}" fill="{c}" fill-opacity="0"/>
            </g>
            <g class="selectpoint">
                <circle id="{eml}" cx="{x - ml + s / 2.0}" cy="{y + h / 2 + s / 2.0}" r="{s / 2.0}" fill="{c}" />
                <circle id="{eml}" cx="{x - ml - tt + t / 2.0}"  cy="{y + h / 2 - tt + t / 2.0}" r="{t / 2.0}" fill="{c}" fill-opacity="0"/>
            </g>
            <g class="selectpoint">
                <circle id="{emr}" cx="{x + w + mr + s / 2.0}" cy="{y + h / 2 + s / 2.0}" r="{s / 2.0}" fill="{c}" />
                <circle id="{emr}" cx="{x + w + mr - tt + t / 2.0}" cy="{y + h / 2 - tt + t / 2.0}" r="{t / 2.0}" fill="{c}" fill-opacity="0"/>
            </g>
            <g class="selectpoint">
                <circle id="{ebl}" cx="{x - ml + s / 2.0}" cy="{y + h + mb + s / 2.0}" r="{s / 2.0}" fill="{c}" />
                <circle id="{ebl}" cx="{x - ml - tt + t / 2.0}"  cy="{y + h + mb - tt + t / 2.0}" r="{t / 2.0}" fill="{c}" fill-opacity="0"/>
            </g>
            <g class="selectpoint">
                <circle id="{ebm}" cx="{x + w / 2 - mm + s / 2.0}" cy="{y + h + mb + s / 2.0}" r="{s / 2.0}" fill="{c}" />
                <circle id="{ebm}" cx="{x + w / 2 - mm - tt + t / 2.0}" cy="{y + h + mb - tt + t / 2.0}" r="{t / 2.0}" fill="{c}" fill-opacity="0"/>
            </g>
            <g class="selectpoint">
                <circle id="{ebr}" cx="{x + w + mr + s / 2.0}" cy="{y + h + mb + s / 2.0}" r="{s / 2.0}" fill="{c}" />
                <circle id="{ebr}" cx="{x + w + mr - tt + t / 2.0}" cy="{y + h + mb - tt + t / 2.0}" r="{t / 2.0}" fill="{c}" fill-opacity="0"/>
            </g>
            """;
    }

    static string IconName(Parsing.NodeType type) =>
        type.Text switch
        {
            "Solution" => "SolutionIcon",
            "Externals" => "ExternalsIcon",
            "Assembly" => "ModuleIcon",
            "Namespace" => "FilesIcon",
            "Private" => "PrivateIcon",
            "Parent" => "FilesIcon",
            "Type" => "TypeIcon",
            "Member" => "MemberIcon",
            _ => "ModuleIcon",
        };

    readonly record struct ContainerHeader(Pos IconPos, double IconSize, Pos TextPos, double FontSize);
}
