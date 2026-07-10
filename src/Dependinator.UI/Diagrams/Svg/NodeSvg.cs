using System.Text.RegularExpressions;
using System.Web;
using Dependinator.UI.Diagrams.Icons;
using Dependinator.UI.Modeling.Models;
using Dependinator.UI.Shared.Types;
using static System.FormattableString;

namespace Dependinator.UI.Diagrams.Svg;

static partial class NodeSvg
{
    const int NameIconSize = 9;
    internal const int FontSize = 8; // Also the member icon size, see NodeAnchors
    const int DescriptionFontSize = 6;

    // Name/description text stops scaling with zoom beyond this factor, so zooming in reveals
    // more description text instead of just growing the letters. Containers appear at
    // NodeViewPolicy.MinContainerZoom (2.0), so text still grows a bit "into" the node before
    // plateauing at 24px names and 18px descriptions.
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

    // Fraction of the average glyph advance relative to the font size (shared with NoteSvg).
    internal const double AverageCharWidthFactor = 0.6;

    public static string GetNodeIconSvg(Node node, Rect nodeCanvasRect, double parentZoom)
    {
        var geometry = CalculateIconGeometry(node, nodeCanvasRect, parentZoom);
        var textZoom = TextZoom(parentZoom);
        var fontSize = FontSize * textZoom;
        var textPos = new Pos(geometry.X + geometry.Width / 2, geometry.Y + geometry.Height);

        var layout = new LeafNodeLayout(
            IconRect: geometry,
            TextPos: textPos,
            TextClass: "iconName",
            TextBaseline: "hanging",
            FontSize: fontSize,
            DescriptionClass: "iconDescription",
            DescriptionY: textPos.Y + fontSize + DescriptionLineGap * textZoom,
            DescriptionMaxLines: ScaledMaxLines(IconDescriptionMaxLines, parentZoom, textZoom),
            Bounds: geometry,
            // The name is centered under the icon, so the marker goes half the text width right.
            MarkerPos: new Pos(textPos.X + EstimateTextWidth(node.ShortName, fontSize) / 2, textPos.Y + fontSize / 2)
        );

        return BuildLeafNodeSvg(node, layout, DescriptionFontSize * textZoom);
    }

    public static string GetNodeContainerSvg(Node node, Rect nodeCanvasRect, double parentZoom, string childrenContent)
    {
        var header = CalculateContainerHeader(nodeCanvasRect, parentZoom);
        var elementId = PointerId.FromNode(node.Id).ElementId;
        var (border, background) = NodeColors(node);
        var (nodeOpacity, textOpacity) = HiddenAttributes(node);
        var iconId = IconName(node);
        var strokeWidth = node.IsEditMode ? 10 : node.StrokeWidth;
        var hoverClass = node.IsEditMode ? "hoverableedit" : "hoverable";
        var selectedOverlay = SelectedNodeSvg(node, nodeCanvasRect);

        var innerGeometry = new Rect(0, 0, nodeCanvasRect.Width, nodeCanvasRect.Height);
        var hoverGroup = BuildHoverGroup(elementId, hoverClass, innerGeometry, node.HtmlLongName, node.HtmlDescription);
        var textZoom = TextZoom(parentZoom);
        var descriptionFontSize = DescriptionFontSize * textZoom;
        var descriptionY = header.TextPos.Y + header.FontSize + DescriptionLineGap * textZoom;
        var descriptionWidth = DescriptionWidthForNode(
            nodeCanvasRect.Width - (header.TextPos.X - nodeCanvasRect.X),
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

        return Invariant(
            $"""
            <svg x="{nodeCanvasRect.X:0.##}" y="{nodeCanvasRect.Y:0.##}" width="{nodeCanvasRect.Width:0.##}" height="{nodeCanvasRect.Height:0.##}" viewBox="{0} {0} {nodeCanvasRect.Width:0.##} {nodeCanvasRect.Height:0.##}" xmlns="http://www.w3.org/2000/svg">
              <rect x="{0}" y="{0}" width="{nodeCanvasRect.Width:0.##}" height="{nodeCanvasRect.Height:0.##}" stroke-width="{strokeWidth:0.##}" rx="5" fill="{background}" stroke="{border}" {nodeOpacity}/>
              {hoverGroup}
              {childrenContent}
            </svg>
            <use href="#{iconId}" xlink:href="#{iconId}" x="{header.IconPos.X:0.##}" y="{header.IconPos.Y:0.##}" width="{header.IconSize:0.##}" height="{header.IconSize:0.##}" {textOpacity}/>
            <text x="{header.TextPos.X:0.##}" y="{header.TextPos.Y:0.##}" class="nodeName" dominant-baseline="hanging" font-size="{header.FontSize:0.##}px" {textOpacity}>{node.HtmlShortName}</text>
            {descriptionSvg}
            {selectedOverlay}
            {ManualMarkerSvg(
                node,
                header.TextPos.X + EstimateTextWidth(node.ShortName, header.FontSize),
                header.TextPos.Y + header.FontSize / 2,
                header.FontSize
            )}
            """
        );
    }

    public static string GetMemberNodeSvg(Node node, Rect nodeCanvasRect, double parentZoom)
    {
        var textZoom = TextZoom(parentZoom);
        var fontSize = FontSize * textZoom;
        var member = CalculateMemberNodeLayout(node, nodeCanvasRect, textZoom, fontSize);

        var layout = new LeafNodeLayout(
            IconRect: member.Icon,
            TextPos: member.Text,
            TextClass: "memberName",
            TextBaseline: "middle",
            FontSize: fontSize,
            DescriptionClass: "memberDescription",
            DescriptionY: member.Text.Y + fontSize / 2 + DescriptionLineGap * textZoom,
            DescriptionMaxLines: ScaledMaxLines(MemberDescriptionMaxLines, parentZoom, textZoom),
            Bounds: member.Bounds,
            MarkerPos: new Pos(member.Text.X + EstimateTextWidth(node.ShortName, fontSize), member.Text.Y)
        );

        return BuildLeafNodeSvg(node, layout, DescriptionFontSize * textZoom);
    }

    // The shared icon/name/description/hover/selection/manual-marker layout of a leaf node
    // (an icon node or a member row); GetNodeIconSvg and GetMemberNodeSvg differ only in these
    // values, not in structure.
    readonly record struct LeafNodeLayout(
        Rect IconRect,
        Pos TextPos,
        string TextClass,
        string TextBaseline,
        double FontSize,
        string DescriptionClass,
        double DescriptionY,
        int DescriptionMaxLines,
        Rect Bounds,
        Pos MarkerPos
    );

    static string BuildLeafNodeSvg(Node node, LeafNodeLayout layout, double descriptionFontSize)
    {
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
        var descriptionSvg = BuildDescriptionSvg(
            node,
            layout.TextPos.X,
            layout.DescriptionY,
            descriptionFontSize,
            layout.DescriptionClass,
            textOpacity,
            DescriptionMinWidth,
            layout.DescriptionMaxLines
        );

        return Invariant(
            $"""
            <use href="#{iconId}" xlink:href="#{iconId}" x="{layout.IconRect.X:0.##}" y="{layout.IconRect.Y:0.##}" width="{layout.IconRect.Width:0.##}" height="{layout.IconRect.Height:0.##}" {nodeOpacity} />
            <text x="{layout.TextPos.X:0.##}" y="{layout.TextPos.Y:0.##}" class="{layout.TextClass}" dominant-baseline="{layout.TextBaseline}" font-size="{layout.FontSize:0.##}px" {textOpacity}>{node.HtmlShortName}</text>
            {descriptionSvg}
            {hoverGroup}
            {selectedOverlay}
            {ManualMarkerSvg(node, layout.MarkerPos.X, layout.MarkerPos.Y, layout.FontSize)}
            """
        );
    }

    public static string GetToLargeNodeContainerSvg(Rect nodeCanvasRect, string childrenContent)
    {
        var (x, y, w, h) = (nodeCanvasRect.X, nodeCanvasRect.Y, nodeCanvasRect.Width, nodeCanvasRect.Height);
        return Invariant(
            $"""
              <svg x="{x:0.##}" y="{y:0.##}" width="{w:0.##}" height="{h:0.##}" viewBox="0 0 {w:0.##} {h:0.##}" xmlns="http://www.w3.org/2000/svg">
                {childrenContent}
              </svg>
            """
        );
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
        var textWidth = Math.Max(fontSize, EstimateTextWidth(node.ShortName, fontSize));

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

    readonly record struct MemberNodeLayout(Rect Bounds, Rect Icon, Pos Text);

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
        return Invariant(
            $"""<text x="{x:0.##}" y="{textCenterY:0.##}" font-size="{glyphSize:0.##}px" fill="{DColors.ManualMarker}" text-anchor="start" dominant-baseline="central" pointer-events="none">&#x270E;</text>"""
        );
    }

    // Rough width of rendered text in the diagram font (average glyph advance), used e.g. to
    // place the manual marker after a name.
    static double EstimateTextWidth(string text, double fontSize) =>
        string.IsNullOrEmpty(text) ? 0 : text.Length * fontSize * AverageCharWidthFactor;

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
                    Invariant(
                        $"""<tspan x="{x:0.##}" dy="{(i == 0 ? firstLineOffset : lineHeight):0.##}">{HttpUtility.HtmlEncode(line)}</tspan>"""
                    )
            )
        );

        return Invariant(
            $"""<text x="{x:0.##}" y="{y:0.##}" class="{cssClass}" font-size="{fontSize:0.##}px" {textOpacity}>{tspans}</text>"""
        );
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
        var text = XmlTagRegex().Replace(description, " ");
        text = WhitespaceRegex().Replace(text, " ").Trim();
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

    [GeneratedRegex("<[^>]*>")]
    private static partial Regex XmlTagRegex();

    [GeneratedRegex(@"\s+")]
    private static partial Regex WhitespaceRegex();

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
        return Invariant(
                $"""
                <g class="{cssClass}" id="{elementId}">
                  <rect id="{elementId}" x="{geometry.X:0.##}" y="{geometry.Y:0.##}" width="{geometry.Width:0.##}" height="{geometry.Height:0.##}" stroke-width="1" rx="2" fill="black" fill-opacity="0" stroke="none"/>
                  <title>{title}</title>
                </g>
                """
            )
            .Trim();
    }

    static string SelectedNodeSvg(Node node, Rect geometry)
    {
        if (!node.IsSelected)
            return "";

        var color = DColors.Selected;
        var x = geometry.X;
        var y = geometry.Y;
        var w = geometry.Width;
        var h = geometry.Height;

        var borderSvg = Invariant(
            $"""
            <rect x="{x - 6:0.##}" y="{y - 6:0.##}" width="{w + 13:0.##}" height="{h
                + 13:0.##}" stroke-width="0.5" rx="0" fill="none" stroke="{color}" stroke-dasharray="5,5"/>
            """
        );

        if (!ViewOptions.IsEditingEnabled)
            return borderSvg; // Show selection border only, no resize handles

        const double HandleRadius = 4; // Visible resize handle
        const double TouchRadius = 15.5; // Invisible, larger hit target for touch/imprecise clicks
        const double HandleMargin = 7; // Distance of a handle center outside the node edge

        var left = x - HandleMargin;
        var centerX = x + w / 2;
        var right = x + w + HandleMargin;
        var top = y - HandleMargin;
        // Middle-row handles sit one radius below the exact vertical middle (kept layout).
        var middleY = y + h / 2 + HandleRadius;
        var bottom = y + h + HandleMargin;

        var handles = new (NodeResizeType Type, double X, double Y)[]
        {
            (NodeResizeType.TopLeft, left, top),
            (NodeResizeType.TopMiddle, centerX, top),
            (NodeResizeType.TopRight, right, top),
            (NodeResizeType.MiddleLeft, left, middleY),
            (NodeResizeType.MiddleRight, right, middleY),
            (NodeResizeType.BottomLeft, left, bottom),
            (NodeResizeType.BottomMiddle, centerX, bottom),
            (NodeResizeType.BottomRight, right, bottom),
        };

        var handlesSvg = handles
            .Select(handle =>
            {
                var elementId = PointerId.FromNodeResize(node.Id, handle.Type).ElementId;
                return Invariant(
                    $"""
                    <g class="selectpoint">
                        <circle id="{elementId}" cx="{handle.X:0.##}" cy="{handle.Y:0.##}" r="{HandleRadius}" fill="{color}" />
                        <circle id="{elementId}" cx="{handle.X:0.##}" cy="{handle.Y:0.##}" r="{TouchRadius:0.##}" fill="{color}" fill-opacity="0"/>
                    </g>
                    """
                );
            })
            .Join("\n");

        return $"{borderSvg}\n{handlesSvg}";
    }

    // The icon id used in <use href="#id"> references; Icon owns the node-type→icon mapping.
    internal static string IconName(Node node) => Icon.GetIconName(node);

    readonly record struct ContainerHeader(Pos IconPos, double IconSize, Pos TextPos, double FontSize);
}
