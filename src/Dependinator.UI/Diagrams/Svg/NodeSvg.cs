using System.Globalization;
using Dependinator.UI.Diagrams.Icons;
using Dependinator.UI.Diagrams.Interaction;
using Dependinator.UI.Modeling.Models;
using Dependinator.UI.Shared.Types;
using static Dependinator.UI.Diagrams.Svg.SvgText;

// A node drawn as an icon or as an open container, and the parts every node shares: the hover
// group, the selection outline and handles, the link handle and the node's colors. Member rows
// are MemberRowSvg's, type cards MemberCardSvg's.
namespace Dependinator.UI.Diagrams.Svg;

static class NodeSvg
{
    const int NameIconSize = 9;

    // Drag-to-link handle at an icon node's right edge (revealed on hover, editing mode only).
    // The gap clears the selection resize handles (HandleMargin 7 + HandleRadius 4) so the
    // handle is not drawn on top of the middle-right resize handle.
    internal const double LinkHandleGap = 14; // Distance from the icon's right edge to the handle center
    const double LinkHandleRadius = 7; // Visible handle dot
    const double LinkHandleTouchRx = 17; // Invisible touch ellipse, bridges back to the icon edge
    const double LinkHandleTouchRy = 11;

    public static string GetNodeIconSvg(
        Node node,
        Rect nodeCanvasRect,
        double parentZoom,
        bool isEditing = false,
        NodeProblems? problems = null,
        int hiddenChildCount = 0
    )
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
            MarkerPos: new Pos(textPos.X + EstimateTextWidth(node.DisplayName, fontSize) / 2, textPos.Y + fontSize / 2),
            LinkHandlePos: new Pos(geometry.X + geometry.Width + LinkHandleGap, geometry.Y + geometry.Height / 2)
        );

        return BuildLeafNodeSvg(node, layout, DescriptionFontSize * textZoom, isEditing)
            + BadgeSvg.Problem(problems, BadgeSvg.VisibleIconRect(geometry), textZoom)
            + BadgeSvg.Hidden(hiddenChildCount, BadgeSvg.VisibleIconRect(geometry), textZoom);
    }

    public static string GetNodeContainerSvg(
        Node node,
        Rect nodeCanvasRect,
        double parentZoom,
        string childrenContent,
        bool isEditing = false,
        NodeProblems? problems = null,
        int hiddenChildCount = 0
    )
    {
        var header = CalculateContainerHeader(nodeCanvasRect, parentZoom);
        var elementId = PointerId.FromNode(node.Id).ElementId;
        var (border, background) = NodeColors(node);
        var backgroundOpacity = NodeViewPolicy.ContainerBackgroundOpacity(node, parentZoom);
        var (nodeOpacity, textOpacity) = HiddenAttributes(node);
        var iconId = IconName(node);
        var strokeWidth = node.IsEditMode ? 10 : node.StrokeWidth;
        var hoverClass = node.IsEditMode ? "hoverableedit" : "hoverable";
        var selectedOverlay = SelectedNodeSvg(node, nodeCanvasRect, isEditing);

        var innerGeometry = new Rect(0, 0, nodeCanvasRect.Width, nodeCanvasRect.Height);
        var hoverGroup = BuildHoverGroup(elementId, hoverClass, innerGeometry, TitleName(node));
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

        return string.Create(
            CultureInfo.InvariantCulture,
            $"""
            <svg x="{nodeCanvasRect.X:0.##}" y="{nodeCanvasRect.Y:0.##}" width="{nodeCanvasRect.Width:0.##}" height="{nodeCanvasRect.Height:0.##}" viewBox="{0} {0} {nodeCanvasRect.Width:0.##} {nodeCanvasRect.Height:0.##}" xmlns="http://www.w3.org/2000/svg">
              <rect x="{0}" y="{0}" width="{nodeCanvasRect.Width:0.##}" height="{nodeCanvasRect.Height:0.##}" stroke-width="{strokeWidth:0.##}" rx="5" fill="{background}" fill-opacity="{backgroundOpacity:0.##}" stroke="{border}" {nodeOpacity}/>
              {hoverGroup}
              {childrenContent}
            </svg>
            <use href="#{iconId}" x="{header.IconPos.X:0.##}" y="{header.IconPos.Y:0.##}" width="{header.IconSize:0.##}" height="{header.IconSize:0.##}" {textOpacity}/>
            <text x="{header.TextPos.X:0.##}" y="{header.TextPos.Y:0.##}" class="nodeName" dominant-baseline="hanging" font-size="{header.FontSize:0.##}px" {textOpacity}>{node.HtmlDisplayName}</text>
            {descriptionSvg}
            {selectedOverlay}
            {ManualMarkerSvg(
                node,
                header.TextPos.X + EstimateTextWidth(node.DisplayName, header.FontSize),
                header.TextPos.Y + header.FontSize / 2,
                header.FontSize
            )}
            {BadgeSvg.Problem(problems, nodeCanvasRect, textZoom)}
            {BadgeSvg.Hidden(hiddenChildCount, nodeCanvasRect, textZoom)}
            """
        );
    }

    // The shared icon/name/description/hover/selection/manual-marker layout of a leaf node
    // (an icon node or a member row); GetNodeIconSvg and MemberRowSvg.GetMemberNodeSvg differ only in these
    // values, not in structure.
    internal readonly record struct LeafNodeLayout(
        Rect IconRect,
        Pos TextPos,
        string TextClass,
        string TextBaseline,
        double FontSize,
        string DescriptionClass,
        double DescriptionY,
        int DescriptionMaxLines,
        Rect Bounds,
        Pos MarkerPos,
        Pos? LinkHandlePos = null,
        string TextSuffix = "",
        string TextAttributes = "",
        bool IsPrivateBadge = false
    );

    internal static string BuildLeafNodeSvg(
        Node node,
        LeafNodeLayout layout,
        double descriptionFontSize,
        bool isEditing
    )
    {
        var iconId = IconName(node);
        var elementId = PointerId.FromNode(node.Id).ElementId;
        var (nodeOpacity, textOpacity) = HiddenAttributes(node);
        var hoverGroup = BuildHoverGroup(
            elementId,
            "hoverable",
            layout.Bounds,
            TitleName(node),
            LinkHandleSvg(node, layout.LinkHandlePos, isEditing),
            isLinePreview: true
        );
        var selectedOverlay = SelectedNodeSvg(node, layout.Bounds, isEditing);
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

        return string.Create(
            CultureInfo.InvariantCulture,
            $"""
            <use href="#{iconId}" x="{layout.IconRect.X:0.##}" y="{layout.IconRect.Y:0.##}" width="{layout.IconRect.Width:0.##}" height="{layout.IconRect.Height:0.##}" {nodeOpacity} />{(
                layout.IsPrivateBadge ? BadgeSvg.Private(layout.IconRect, nodeOpacity) : ""
            )}
            <text x="{layout.TextPos.X:0.##}" y="{layout.TextPos.Y:0.##}" class="{layout.TextClass}" dominant-baseline="{layout.TextBaseline}" font-size="{layout.FontSize:0.##}px" {textOpacity} {layout.TextAttributes}>{node.HtmlDisplayName}{layout.TextSuffix}</text>
            {descriptionSvg}
            {hoverGroup}
            {selectedOverlay}
            {ManualMarkerSvg(node, layout.MarkerPos.X, layout.MarkerPos.Y, layout.FontSize)}
            """
        );
    }

    public static string GetTooLargeNodeContainerSvg(Rect nodeCanvasRect, string childrenContent)
    {
        var (x, y, w, h) = (nodeCanvasRect.X, nodeCanvasRect.Y, nodeCanvasRect.Width, nodeCanvasRect.Height);
        return string.Create(
            CultureInfo.InvariantCulture,
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

    internal static (string NodeOpacity, string TextOpacity) HiddenAttributes(Node node) =>
        !node.IsVisible ? ("opacity=\"0.1\"", "opacity=\"0.3\"") : ("", "");

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
        return string.Create(
            CultureInfo.InvariantCulture,
            $"""<text x="{x:0.##}" y="{textCenterY:0.##}" font-size="{glyphSize:0.##}px" fill="{DColors.ManualMarker}" text-anchor="start" dominant-baseline="central" pointer-events="none">&#x270E;</text>"""
        );
    }

    // Whether the node is drawn in its own colors (edit mode or a user-set color) rather than its
    // kind's default (the random color, or a card's lavender: CardColors).
    internal static bool HasOwnColors(Node node) =>
        node.IsEditMode || node.CustomColor is { } customColor && DColors.IsCustomNodeColor(customColor);

    internal static (string Border, string Background) NodeColors(Node node)
    {
        if (node.IsEditMode)
            return (DColors.EditNodeBorder, DColors.EditNodeBackground);

        // A user-selected container color overrides the auto-assigned one; unknown (e.g. stale
        // persisted) names fall back to the auto color.
        if (node.CustomColor is { } customColor && DColors.IsCustomNodeColor(customColor))
            return DColors.CustomNodeColorByName(customColor);

        return DColors.NodeColorByName(node.Color);
    }

    // The hover group's data-name: the real long name (the identity, also what the e2e locators
    // match), followed by the user's display name when one is set. The tooltip itself is not in
    // the SVG: the hover card (TooltipService, initTooltips in jsInterop.js) is built only when the pointer rests.
    internal static string TitleName(Node node) =>
        node.HtmlAlias is null ? node.HtmlLongName : $"{node.HtmlLongName} ({node.HtmlAlias})";

    internal static string BuildHoverGroup(
        string elementId,
        string cssClass,
        Rect geometry,
        string htmlName,
        string extraContent = "",
        bool isLinePreview = false
    )
    {
        // data-line-preview marks the nodes whose lines the hover preview lights (linePreview in
        // jsInterop.js): icons and member rows. Not open containers, which the pointer is
        // always over when zoomed in, so they would keep the preview on.
        var previewAttribute = isLinePreview ? " data-line-preview=\"1\"" : "";
        return string.Create(
                CultureInfo.InvariantCulture,
                $"""
                <g class="{cssClass}" id="{elementId}" data-name="{htmlName}"{previewAttribute}>
                  <rect id="{elementId}" x="{geometry.X:0.##}" y="{geometry.Y:0.##}" width="{geometry.Width:0.##}" height="{geometry.Height:0.##}" stroke-width="1" rx="2" fill="black" fill-opacity="0" stroke="none"/>
                  {extraContent}
                </g>
                """
            )
            .Trim();
    }

    // A drag-to-link handle at the node's right edge, revealed on hover (see the .linkhandle CSS
    // in Canvas.razor). It sits inside the hover group so hovering the handle itself keeps it
    // visible. Dragging it to another node creates a manual link (see InteractionService).
    // Not shown while the node is selected: the selection's resize handles own the node edge
    // (their large touch circles would swallow the handle's presses).
    internal static string LinkHandleSvg(Node node, Pos? pos, bool isEditing)
    {
        if (pos is null || !isEditing || node.IsSelected)
            return "";

        var (x, y) = (pos.X, pos.Y);
        var elementId = PointerId.FromNodeLinkHandle(node.Id).ElementId;
        // Visible dot + plus glyph, plus an invisible touch ellipse that stretches back to the
        // icon edge so the cursor never crosses a dead zone between node and handle (no hover
        // flicker).
        return string.Create(
            CultureInfo.InvariantCulture,
            $"""
            <g class="linkhandle">
              <circle id="{elementId}" cx="{x:0.##}" cy="{y:0.##}" r="{LinkHandleRadius}" fill="{DColors.Selected}"/>
              <path d="M {x - 3:0.##} {y:0.##} h 6 M {x:0.##} {y
                - 3:0.##} v 6" stroke="white" stroke-width="1.5" fill="none" pointer-events="none"/>
              <ellipse id="{elementId}" cx="{x:0.##}" cy="{y:0.##}" rx="{LinkHandleTouchRx}" ry="{LinkHandleTouchRy}" fill="{DColors.Selected}" fill-opacity="0"/>
            </g>
            """
        );
    }

    internal static string SelectedNodeSvg(Node node, Rect geometry, bool isEditing)
    {
        if (!node.IsSelected)
            return "";

        var color = DColors.Selected;
        var x = geometry.X;
        var y = geometry.Y;
        var w = geometry.Width;
        var h = geometry.Height;

        var borderSvg = string.Create(
            CultureInfo.InvariantCulture,
            $"""
            <rect x="{x - 6:0.##}" y="{y - 6:0.##}" width="{w + 13:0.##}" height="{h
                + 13:0.##}" stroke-width="0.5" rx="0" fill="none" stroke="{color}" stroke-dasharray="5,5"/>
            """
        );

        if (!isEditing)
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
                return string.Create(
                    CultureInfo.InvariantCulture,
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
