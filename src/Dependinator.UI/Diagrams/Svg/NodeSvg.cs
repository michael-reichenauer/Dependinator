using System.Text.RegularExpressions;
using System.Web;
using Dependinator.Core;
using Dependinator.UI.Diagrams.Icons;
using Dependinator.UI.Modeling.Models;
using Dependinator.UI.Shared.Types;

namespace Dependinator.UI.Diagrams.Svg;

class NodeSvg
{
    static bool? IsEditingEnabledManual = null;
    const double MaxNodeZoom = 8 * 1 / Node.DefaultContainerZoom; // To large to be seen
    const double MinContainerZoom = 2.0;
    const int NameIconSize = 9;
    const int FontSize = 8;
    const int DescriptionFontSize = 6;

    // Name/description text stops scaling with zoom beyond this factor, so zooming in reveals
    // more description text instead of just growing the letters. Containers appear at
    // MinContainerZoom (2.0), so text still grows a bit "into" the node before plateauing at
    // 24px names and 18px descriptions.
    const double MaxTextZoom = 3.0;
    const int DescriptionMinWidth = 25;
    const int DescriptionMaxWidth = 100;
    const int DescriptionMaxLines = 7;
    const int IconDescriptionMaxLines = 5;
    const int MemberDescriptionMaxLines = 1;
    const double DescriptionCharWidthFactor = 0.45;
    const double DescriptionLineHeightFactor = 1.2;
    const double DescriptionLineGap = 1;

    // Offset (in em) from the text's y down to the first line's alphabetic baseline. This lets us
    // top-align the description using the universally-supported alphabetic baseline instead of
    // `dominant-baseline: hanging`, which WebKit (iPad Safari) does not apply to <tspan> children.
    const double DescriptionFirstLineOffsetFactor = 0.8;
    const double MemberTextGap = 4;
    const double MemberHorizontalPadding = 4;
    const double MemberAverageCharWidthFactor = 0.6;

    public static bool ShowHiddenNodes { get; private set; } = true;
    public static bool IsEditingEnabled => IsEditingEnabledManual ?? !Build.IsStandaloneWasm;

    public static void SetShowHiddenNodes(bool show) => ShowHiddenNodes = show;

    public static void SetIsEditingEnabled(bool enabled) => IsEditingEnabledManual = enabled;

    public static bool IsToLargeToBeSeen(double zoom) => zoom > MaxNodeZoom;

    public static bool IsShowIcon(Parsing.NodeType nodeType, double zoom) =>
        nodeType.IsMember || zoom <= MinContainerZoom;

    public static string GetNodeIconSvg(Node node, Rect nodeCanvasRect, double parentZoom)
    {
        var geometry = CalculateIconGeometry(node, nodeCanvasRect, parentZoom);
        var textZoom = TextZoom(parentZoom);
        var textX = geometry.X + geometry.Width / 2;
        var textY = geometry.Y + geometry.Height;
        var fontSize = FontSize * textZoom;
        var iconId = IconName(node);
        var elementId = PointerId.FromNode(node.Id).ElementId;
        var (nodeOpacity, textOpacity) = HiddenAttributes(node);
        var hoverGroup = BuildHoverGroup(elementId, "hoverable", geometry, node.HtmlLongName, node.HtmlDescription);
        var selectedOverlay = SelectedNodeSvg(node, geometry);
        var descriptionFontSize = DescriptionFontSize * textZoom;
        var descriptionY = textY + fontSize + DescriptionLineGap * textZoom;
        var descriptionSvg = BuildDescriptionSvg(
            node,
            textX,
            descriptionY,
            descriptionFontSize,
            "iconDescription",
            textOpacity,
            DescriptionMinWidth,
            ScaledMaxLines(IconDescriptionMaxLines, parentZoom, textZoom)
        );

        return $"""
            <use href="#{iconId}" xlink:href="#{iconId}" x="{geometry.X:0.##}" y="{geometry.Y:0.##}" width="{geometry.Width:0.##}" height="{geometry.Height:0.##}" {nodeOpacity} />
            <text x="{textX:0.##}" y="{textY:0.##}" class="iconName" dominant-baseline="hanging" font-size="{fontSize:0.##}px" {textOpacity} >{node.HtmlShortName}</text>
            {descriptionSvg}
            {hoverGroup}
            {selectedOverlay}
            {ManualMarkerSvg(
                node,
                textX + EstimateNameWidth(node.ShortName, fontSize) / 2,
                textY + fontSize / 2,
                fontSize
            )}
            """;
    }

    public static string GetNodeContainerSvg(Node node, Rect nodeCanvasRect, double parentZoom, string childrenContent)
    {
        var geometry = nodeCanvasRect;
        var header = CalculateContainerHeader(nodeCanvasRect, parentZoom);
        var elementId = PointerId.FromNode(node.Id).ElementId;
        var (border, background) = NodeColors(node);
        var (nodeOpacity, textOpacity) = HiddenAttributes(node);
        var iconId = IconName(node);
        var strokeWidth = node.IsEditMode ? 10 : node.StrokeWidth;
        var hoverClass = node.IsEditMode ? "hoverableedit" : "hoverable";
        var selectedOverlay = SelectedNodeSvg(node, geometry);

        var innerGeometry = new Rect(0, 0, geometry.Width, geometry.Height);
        var hoverGroup = BuildHoverGroup(elementId, hoverClass, innerGeometry, node.HtmlLongName, node.HtmlDescription);
        var textZoom = TextZoom(parentZoom);
        var descriptionFontSize = DescriptionFontSize * textZoom;
        var descriptionY = header.TextPos.Y + header.FontSize + DescriptionLineGap * textZoom;
        var descriptionWidth = DescriptionWidthForNode(
            geometry.Width - (header.TextPos.X - geometry.X),
            descriptionFontSize
        );
        var descriptionSvg = BuildDescriptionSvg(
            node,
            header.TextPos.X,
            descriptionY,
            descriptionFontSize,
            "nodeDescription",
            textOpacity,
            descriptionWidth,
            ScaledMaxLines(DescriptionMaxLines, parentZoom, textZoom)
        );

        return $"""
            <svg x="{geometry.X:0.##}" y="{geometry.Y:0.##}" width="{geometry.Width:0.##}" height="{geometry.Height:0.##}" viewBox="{0} {0} {geometry.Width:0.##} {geometry.Height:0.##}" xmlns="http://www.w3.org/2000/svg">
              <rect x="{0}" y="{0}" width="{geometry.Width:0.##}" height="{geometry.Height:0.##}" stroke-width="{strokeWidth}" rx="5" fill="{background}" stroke="{border}" {nodeOpacity}/>
              {hoverGroup}
              {childrenContent}
            </svg>
            <use href="#{iconId}" xlink:href="#{iconId}" x="{header.IconPos.X:0.##}" y="{header.IconPos.Y:0.##}" width="{header.IconSize:0.##}" height="{header.IconSize:0.##}" {textOpacity}/>
            <text x="{header.TextPos.X:0.##}" y="{header.TextPos.Y:0.##}" class="nodeName" dominant-baseline="hanging" font-size="{header.FontSize:0.##}px" {textOpacity}>{node.HtmlShortName}</text>
            {descriptionSvg}
            {selectedOverlay}
            {ManualMarkerSvg(
                node,
                header.TextPos.X + EstimateNameWidth(node.ShortName, header.FontSize),
                header.TextPos.Y + header.FontSize / 2,
                header.FontSize
            )}
            """;
    }

    public static string GetMemberNodeSvg(Node node, Rect nodeCanvasRect, double parentZoom)
    {
        var textZoom = TextZoom(parentZoom);
        var fontSize = FontSize * textZoom;
        var layout = CalculateMemberNodeLayout(node, nodeCanvasRect, textZoom, fontSize);
        var iconId = IconName(node);
        var elementId = PointerId.FromNode(node.Id).ElementId;
        var (nodeOpacity, textOpacity) = HiddenAttributes(node);
        var hoverGroup = BuildHoverGroup(
            elementId,
            "hoverable",
            layout.Bounds,
            node.HtmlLongName,
            node.HtmlDescription
        );
        var selectedOverlay = SelectedNodeSvg(node, layout.Bounds);
        var descriptionFontSize = DescriptionFontSize * textZoom;
        var descriptionY = layout.Text.Y + fontSize / 2 + DescriptionLineGap * textZoom;
        var descriptionSvg = BuildDescriptionSvg(
            node,
            layout.Text.X,
            descriptionY,
            descriptionFontSize,
            "memberDescription",
            textOpacity,
            DescriptionMinWidth,
            ScaledMaxLines(MemberDescriptionMaxLines, parentZoom, textZoom)
        );

        return $"""
            <use href="#{iconId}" xlink:href="#{iconId}" x="{layout.Icon.X:0.##}" y="{layout.Icon.Y:0.##}" width="{layout.Icon.Width:0.##}" height="{layout.Icon.Height:0.##}" {nodeOpacity} />
            <text x="{layout.Text.X:0.##}" y="{layout.Text.Y:0.##}" class="memberName" dominant-baseline="middle" font-size="{fontSize:0.##}px" {textOpacity}>{node.HtmlShortName}</text>
            {descriptionSvg}
            {hoverGroup}
            {selectedOverlay}
            {ManualMarkerSvg(
                node,
                layout.Text.X + EstimateNameWidth(node.ShortName, fontSize),
                layout.Text.Y,
                fontSize
            )}
            """;
    }

    public static string GetToLargeNodeContainerSvg(Rect nodeCanvasRect, string childrenContent)
    {
        var (x, y, w, h) = (nodeCanvasRect.X, nodeCanvasRect.Y, nodeCanvasRect.Width, nodeCanvasRect.Height);
        return $"""
              <svg x="{x:0.##}" y="{y:0.##}" width="{w:0.##}" height="{h:0.##}" viewBox="0 0 {w:0.##} {h:0.##}" xmlns="http://www.w3.org/2000/svg">
                {childrenContent}
              </svg>
            """;
    }

    static Rect CalculateIconGeometry(Node node, Rect nodeCanvasRect, double parentZoom)
    {
        var width = node.Boundary.Width * parentZoom;
        var height = node.Boundary.Height * parentZoom;
        return new Rect(nodeCanvasRect.X, nodeCanvasRect.Y, width, height);
    }

    static MemberNodeLayout CalculateMemberNodeLayout(Node node, Rect nodeCanvasRect, double textZoom, double fontSize)
    {
        var iconSize = fontSize;
        var centerY = nodeCanvasRect.Y + nodeCanvasRect.Height / 2;

        var iconX = nodeCanvasRect.X;
        var iconY = centerY - iconSize / 2;

        var gap = MemberTextGap * textZoom;
        var padding = MemberHorizontalPadding * textZoom;
        var textWidth = EstimateMemberTextWidth(node.ShortName, fontSize);

        var layoutWidth = iconSize + gap + textWidth + padding;
        var layoutHeight = Math.Max(iconSize, fontSize);
        var layoutX = iconX;
        var layoutY = centerY - layoutHeight / 2;

        var textX = iconX + iconSize + gap;
        var textY = centerY;

        var layoutRect = new Rect(layoutX, layoutY, layoutWidth, layoutHeight);
        var iconRect = new Rect(iconX, iconY, iconSize, iconSize);
        var textPos = new Pos(textX, textY);

        return new MemberNodeLayout(layoutRect, iconRect, textPos);
    }

    static double EstimateMemberTextWidth(string shortName, double fontSize)
    {
        if (string.IsNullOrEmpty(shortName))
            return fontSize;

        var approximate = shortName.Length * fontSize * MemberAverageCharWidthFactor;
        return Math.Max(fontSize, approximate);
    }

    readonly record struct MemberNodeLayout(Rect Bounds, Rect Icon, Pos Text);

    readonly record struct MemberAnchorMetrics(
        double Left,
        double Right,
        double CenterX,
        double CenterY,
        double Bottom
    );

    internal enum LineAnchorRole
    {
        Source,
        Target,
    }

    internal enum AnchorPreference
    {
        Default,
        Left,
        Right,
    }

    internal static (double X, double Y) GetLineAnchor(
        Node node,
        LineAnchorRole role,
        AnchorPreference preference = AnchorPreference.Default
    )
    {
        if (node.Type.IsMember)
        {
            var metrics = GetMemberAnchorMetrics(node);
            if (role == LineAnchorRole.Source)
                return (metrics.CenterX, metrics.Bottom + 0.5);

            return preference switch
            {
                AnchorPreference.Right => (metrics.Right, metrics.CenterY),
                _ => (metrics.Left - 0.5, metrics.CenterY),
            };
        }

        var boundary = node.Boundary;
        var centerY = boundary.Y + boundary.Height / 2.0;

        if (role == LineAnchorRole.Source)
        {
            return preference switch
            {
                AnchorPreference.Left => (boundary.X, centerY),
                _ => (boundary.X + boundary.Width, centerY),
            };
        }

        return preference switch
        {
            AnchorPreference.Right => (boundary.X + boundary.Width, centerY),
            _ => (boundary.X, centerY),
        };
    }

    static MemberAnchorMetrics GetMemberAnchorMetrics(Node node)
    {
        var boundary = node.Boundary;
        var iconSize = (double)FontSize;
        var left = boundary.X;
        var right = boundary.X + iconSize;
        var centerX = boundary.X + iconSize / 2.0;
        var centerY = boundary.Y + boundary.Height / 2.0;
        var bottom = centerY + iconSize / 2.0;
        return new MemberAnchorMetrics(left, right, centerX, centerY, bottom);
    }

    static ContainerHeader CalculateContainerHeader(Rect nodeCanvasRect, double parentZoom)
    {
        var textZoom = TextZoom(parentZoom);
        var iconSize = NameIconSize * textZoom;
        var iconPosition = new Pos(nodeCanvasRect.X, nodeCanvasRect.Y + nodeCanvasRect.Height + 1 * textZoom);
        var textPosition = new Pos(
            nodeCanvasRect.X + (NameIconSize + 1) * textZoom,
            nodeCanvasRect.Y + nodeCanvasRect.Height + 2 * textZoom
        );
        var fontSize = FontSize * textZoom;
        return new ContainerHeader(iconPosition, iconSize, textPosition, fontSize);
    }

    // Effective zoom for text and header icons: follows the diagram zoom until MaxTextZoom,
    // then stays capped so text keeps a readable size instead of growing with the node.
    static double TextZoom(double parentZoom) => Math.Min(parentZoom, MaxTextZoom);

    // Once the font size is capped, further zooming leaves unused space below the node (layout
    // distances keep scaling while text does not). Convert that surplus into extra description
    // lines, keeping the same pixel footprint as the uncapped layout would have used.
    static int ScaledMaxLines(int maxLines, double parentZoom, double textZoom) =>
        textZoom <= 0 ? maxLines : (int)(maxLines * parentZoom / textZoom);

    static (string NodeOpacity, string TextOpacity) HiddenAttributes(Node node) =>
        node.IsHidden ? ("opacity=\"0.1\"", "opacity=\"0.3\"") : ("", "");

    // A small pencil glyph placed just after the node's name label, marking a manually added
    // (user-drawn) node so it reads as "hand-drawn/editable" (not a status). Empty for parsed
    // nodes. textEndX is the x just past the end of the name text; textCenterY its vertical center.
    static string ManualMarkerSvg(Node node, double textEndX, double textCenterY, double fontSize)
    {
        if (!node.IsManual)
            return "";

        var glyphSize = fontSize * 0.95;
        var x = textEndX + fontSize * 0.35;
        // U+270E LOWER RIGHT PENCIL
        return $"""<text x="{x:0.##}" y="{textCenterY:0.##}" font-size="{glyphSize:0.##}px" fill="{DColors.ManualMarker}" text-anchor="start" dominant-baseline="central" pointer-events="none">&#x270E;</text>""";
    }

    // Rough width of a rendered name in the diagram font, used to place the manual marker after it.
    static double EstimateNameWidth(string text, double fontSize) =>
        string.IsNullOrEmpty(text) ? 0 : text.Length * fontSize * MemberAverageCharWidthFactor;

    static (string Border, string Background) NodeColors(Node node)
    {
        var (border, background) = DColors.NodeColorByName(node.Color);
        if (node.IsEditMode)
            return (DColors.EditNodeBorder, DColors.EditNodeBackground);
        return (border, background);
    }

    static string BuildDescriptionSvg(
        Node node,
        double x,
        double y,
        double fontSize,
        string cssClass,
        string textOpacity,
        int maxWidth,
        int maxLines
    )
    {
        var lines = GetDescriptionLines(node.Description, maxWidth, maxLines);
        if (lines.Count == 0)
            return "";

        var lineHeight = fontSize * DescriptionLineHeightFactor;
        var firstLineOffset = fontSize * DescriptionFirstLineOffsetFactor;
        var tspans = string.Join(
            "\n",
            lines.Select(
                (line, i) =>
                    $"""<tspan x="{x:0.##}" dy="{(i == 0 ? firstLineOffset : lineHeight):0.##}">{HttpUtility.HtmlEncode(line)}</tspan>"""
            )
        );

        return $"""<text x="{x:0.##}" y="{y:0.##}" class="{cssClass}" font-size="{fontSize:0.##}px" {textOpacity}>{tspans}</text>""";
    }

    // Estimate how many characters fit across a node of the given pixel width, so wider
    // (zoomed-in) containers wrap at a wider line than small icon nodes. Clamped to a
    // readable floor and a sane cap.
    internal static int DescriptionWidthForNode(double availableWidth, double fontSize)
    {
        var charWidth = fontSize * DescriptionCharWidthFactor;
        if (charWidth <= 0)
            return DescriptionMinWidth;

        var chars = (int)(availableWidth / charWidth);
        return Math.Clamp(chars, DescriptionMinWidth, DescriptionMaxWidth);
    }

    internal static IReadOnlyList<string> GetDescriptionLines(string? description, int maxWidth, int maxLines)
    {
        if (string.IsNullOrWhiteSpace(description))
            return [];

        // Collapse to a single line and strip XML-doc tags like <summary>.
        var text = Regex.Replace(description, "<[^>]*>", " ");
        text = Regex.Replace(text, "\\s+", " ").Trim();
        if (text.Length == 0)
            return [];

        var lines = WrapText(text, maxWidth);
        if (lines.Count <= maxLines)
            return lines;

        // Truncate to maxLines and mark the last kept line with an ellipsis.
        lines = lines.Take(maxLines).ToList();
        var last = lines[^1];
        if (last.Length + 1 > maxWidth)
            last = last[..(maxWidth - 1)].TrimEnd();
        lines[^1] = last + "…";
        return lines;
    }

    static List<string> WrapText(string text, int maxWidth)
    {
        var lines = new List<string>();
        var current = "";

        foreach (var word in text.Split(' ', StringSplitOptions.RemoveEmptyEntries))
        {
            var remaining = word;

            // Hard-break words that are longer than a full line.
            while (remaining.Length > maxWidth)
            {
                if (current.Length > 0)
                {
                    lines.Add(current);
                    current = "";
                }
                lines.Add(remaining[..maxWidth]);
                remaining = remaining[maxWidth..];
            }

            if (remaining.Length == 0)
                continue;

            if (current.Length == 0)
                current = remaining;
            else if (current.Length + 1 + remaining.Length <= maxWidth)
                current += " " + remaining;
            else
            {
                lines.Add(current);
                current = remaining;
            }
        }

        if (current.Length > 0)
            lines.Add(current);

        return lines;
    }

    static string BuildHoverGroup(
        string elementId,
        string cssClass,
        Rect geometry,
        string htmlLongName,
        string? htmlDescription
    )
    {
        var title = string.IsNullOrWhiteSpace(htmlDescription) ? htmlLongName : $"{htmlLongName}\n\n{htmlDescription}";
        return $"""
            <g class="{cssClass}" id="{elementId}">
              <rect id="{elementId}" x="{geometry.X:0.##}" y="{geometry.Y:0.##}" width="{geometry.Width:0.##}" height="{geometry.Height:0.##}" stroke-width="1" rx="2" fill="black" fill-opacity="0" stroke="none"/>
              <title>{title}</title>
            </g>
            """.Trim();
    }

    static string SelectedNodeSvg(Node node, Rect geometry)
    {
        if (!node.IsSelected)
            return "";

        if (!IsEditingEnabled)
        {
            // Show selection border only, no resize handles
            return $"""
                <rect x="{geometry.X - 6}" y="{geometry.Y - 6}" width="{geometry.Width + 13:0.##}" height="{geometry.Height
                    + 13:0.##}" stroke-width="0.5" rx="0" fill="none" stroke="{DColors.Selected}" stroke-dasharray="5,5"/>
                """;
        }

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

    internal static string IconName(Node node)
    {
        // A user-selected icon overrides the node-type default; unknown (e.g. stale persisted)
        // names fall back to the default.
        if (node.CustomIconName is { } customIconName && IconLibrary.Contains(customIconName))
            return customIconName;

        return node.Type switch
        {
            Parsing.NodeType.EventMember => "Event",
            Parsing.NodeType.FieldMember => "Field",
            Parsing.NodeType.PropertyMember => "Property",
            Parsing.NodeType.MethodMember => "Method",
            Parsing.NodeType.ConstructorMember => "Constructor",
            Parsing.NodeType.Solution => "Solution",
            Parsing.NodeType.Externals => "Externals",
            Parsing.NodeType.Assembly => "Assembly",
            Parsing.NodeType.Namespace => "Namespace",
            // The Roslyn source parser doesn't emit Namespace nodes; namespace containers are
            // rebuilt as implicit Parent nodes (see StructureService.GetOrCreateParent), so Parent
            // also renders as a namespace. (The Files icon is kept in the library for future use.)
            Parsing.NodeType.Parent => "Namespace",
            Parsing.NodeType.Type => "Type",
            Parsing.NodeType.ClassType => "Type",
            Parsing.NodeType.InterfaceType => "Interface",
            Parsing.NodeType.EnumType => "Enum",
            Parsing.NodeType.StructType => "Struct",
            Parsing.NodeType.RecordType => "Record",

            _ => "Module",
        };
    }

    readonly record struct ContainerHeader(Pos IconPos, double IconSize, Pos TextPos, double FontSize);
}
