using System.Globalization;
using Dependinator.UI.Diagrams.Interaction;
using Dependinator.UI.Modeling;
using Dependinator.UI.Modeling.Models;
using Dependinator.UI.Shared.Types;
using static Dependinator.UI.Diagrams.Svg.SvgText;
using NodeType = Dependinator.Core.Parsing.NodeType;

namespace Dependinator.UI.Diagrams.Svg;

// The member card's drawing: behind an open container's member rows, a card with a heading per
// section (see MemberCard); and, while the rows are still too small to read, a summary card in
// place of the container (see NodeViewPolicy.IsMemberSummary).
static class MemberCardSvg
{
    const double HeadingFontSize = 6.5; // In member units, like the 8-unit member names
    const double CardCornerRadius = 4;
    const double CardStrokeWidth = 0.6;

    // The card and its section headings, in the container's children canvas space.
    public static string GetMemberCardSvg(
        Node node,
        Pos childrenOffset,
        double childrenZoom,
        Func<Node, bool>? isRowShown = null,
        bool isCardNode = false
    )
    {
        var card = MemberCard.Bounds(node, isRowShown);
        if (card == Rect.None)
            return "";

        double X(double x) => childrenOffset.X + x * childrenZoom;
        double Y(double y) => childrenOffset.Y + y * childrenZoom;
        var textZoom = TextZoom(childrenZoom);
        var fontSize = HeadingFontSize * textZoom;
        var (nodeOpacity, textOpacity) = NodeSvg.HiddenAttributes(node);

        var svg = new System.Text.StringBuilder();
        // A card node is the card itself (GetCardNodeSvg); other containers get one behind the rows.
        if (!isCardNode)
            svg.Append(
                string.Create(
                    CultureInfo.InvariantCulture,
                    $"""<rect class="memberCard" x="{X(card.X):0.##}" y="{Y(card.Y):0.##}" width="{card.Width * childrenZoom:0.##}" height="{card.Height
                    * childrenZoom:0.##}" rx="{CardCornerRadius * childrenZoom:0.##}" fill="{DColors.CardBackground}" stroke="{DColors.CardBorder}" stroke-width="{CardStrokeWidth
                    * textZoom:0.##}" pointer-events="none" {nodeOpacity}/>"""
                )
            );

        var headings = MemberCard.Headings(node, isRowShown);
        // In a card node a divider in the first or last column reaches the box's edge.
        var (firstColumnX, lastColumnX) =
            headings.Count > 0 ? (headings.Min(h => h.X), headings.Max(h => h.X)) : (0, 0);
        var boxLeft = -node.ContainerOffset.X / node.ContainerZoom;
        var boxRight = (node.Boundary.Width - node.ContainerOffset.X) / node.ContainerZoom;
        foreach (var heading in headings)
        {
            if (!heading.IsColumnTop)
            {
                var x1 = isCardNode && heading.X == firstColumnX ? boxLeft : heading.X - MemberCard.Padding;
                var x2 =
                    isCardNode && heading.X == lastColumnX ? boxRight : heading.X + heading.Width + MemberCard.Padding;
                svg.Append(
                    string.Create(
                        CultureInfo.InvariantCulture,
                        $"""<line x1="{X(x1):0.##}" y1="{Y(heading.Y):0.##}" x2="{X(x2):0.##}" y2="{Y(heading.Y):0.##}" stroke="{DColors.CardDivider}" stroke-width="{CardStrokeWidth
                            * textZoom:0.##}" pointer-events="none" {nodeOpacity}/>"""
                    )
                );
            }

            // The heading's baseline a little above the row below it, like a compartment's title.
            var baselineY = Y(heading.Y + MemberCard.HeadingHeight * 0.7);
            svg.Append(
                string.Create(
                    CultureInfo.InvariantCulture,
                    $"""<text x="{X(heading.X):0.##}" y="{baselineY:0.##}" class="memberHeading" font-size="{fontSize:0.##}px" letter-spacing="{fontSize
                        * 0.08:0.##}px" {textOpacity}>{heading.Title.ToUpperInvariant()}</text>"""
                )
            );
        }

        return svg.ToString();
    }

    // The card's "+ N private" row (MemberCard.HasPrivateToggle): a click shows or folds the
    // private rows (InteractionService, PointerId.IsPrivateToggle).
    // Drawn after the lines, which would otherwise take its clicks (lines of folded rows start there).
    public static string GetPrivateToggleSvg(
        Node node,
        Pos childrenOffset,
        double childrenZoom,
        Func<Node, bool>? isRowShown = null
    )
    {
        double X(double x) => childrenOffset.X + x * childrenZoom;
        double Y(double y) => childrenOffset.Y + y * childrenZoom;
        var textZoom = TextZoom(childrenZoom);
        if (!MemberCard.IsToggleShown(node, isRowShown))
            return "";
        var rect = MemberCard.PrivateToggleRect(node);
        var count = MemberCard.PrivateCount(node, isRowShown);
        var isFolded = MemberCard.IsPrivateFolded(node);
        var text = isFolded ? $"+ {count} private" : $"hide {count} private";
        var tooltip = isFolded ? "Show the private members" : "Fold the private members";
        var elementId = PointerId.FromPrivateToggle(node.Id).ElementId;
        var fontSize = FontSize * textZoom;
        var (x, y, w, h) = (X(rect.X), Y(rect.Y), rect.Width * childrenZoom, rect.Height * childrenZoom);
        return string.Create(
            CultureInfo.InvariantCulture,
            $"""<g class="privateToggle" id="{elementId}" data-testid="private-toggle" data-node="{Encode(node.ShortName)}" data-folded="{(isFolded ? "true" : "false")}"><rect id="{elementId}" x="{x:0.##}" y="{y:0.##}" width="{w:0.##}" height="{h:0.##}" fill="black" fill-opacity="0"/><text id="{elementId}" x="{x + fontSize * 1.5:0.##}" y="{y + h / 2:0.##}" class="privateToggleText" dominant-baseline="middle" font-size="{fontSize:0.##}px">{Encode(text)}</text><title>{tooltip}</title></g>"""
        );
    }

    // The summary card: a type (or a resource with auxiliary rows) opened far enough to show more
    // than an icon but not yet its readable rows. A header with the icon, name and kind, the member
    // counts, and as many members as fit, public first, with "+ N more". It is a leaf like an
    // icon: lines attach to its edges, and clicking it selects the node.
    public static string GetMemberSummarySvg(
        Node node,
        Rect rect,
        double parentZoom,
        bool isEditing = false,
        NodeProblems? problems = null,
        int hiddenChildCount = 0,
        Func<Node, bool>? isRowShown = null
    )
    {
        // Sizes for a 180 px card: it shows from 120 px (the smallest type) to 360 px (the largest;
        // NodeViewPolicy.OpenSize), and its text stops growing at about 230 px, near the size the
        // rows have when the full card replaces it, so growing it reveals more rows instead.
        var u = Math.Min(rect.Width / 180, 1.3);
        var (x, y, w, h) = (rect.X, rect.Y, rect.Width, rect.Height);
        var (nodeOpacity, textOpacity) = NodeSvg.HiddenAttributes(node);
        var elementId = PointerId.FromNode(node.Id).ElementId;

        var headerHeight = MemberCard.CardHeaderHeight * u;
        var smallSize = 8.5 * u;
        var rowSize = 9 * u;
        var rowPitch = 13 * u;
        var padding = 9 * u;

        var svg = new System.Text.StringBuilder();
        svg.Append(CardFrameSvg(node, rect, u, u));
        var members = node.Children.Where(c => c.Type.IsMember && (isRowShown?.Invoke(c) ?? true)).ToList();
        var sections = MemberCard.Sections(members, (a, b) => string.CompareOrdinal(a.Name, b.Name));
        // The rows: public members first (in section order), then private ones, then the nested
        // types under a heading of their own. As many as fit, with "+ N more".
        var rows = sections.SelectMany(s => s.Rows).OrderBy(m => m.IsPrivate ?? false).Cast<Node?>().ToList();
        var nested = node
            .Children.Where(c => !c.Type.IsMember && !NodeViewPolicy.IsAnnotation(c) && (isRowShown?.Invoke(c) ?? true))
            .OrderBy(c => c.IsPrivate ?? false)
            .ThenBy(c => c.DisplayName, StringComparer.OrdinalIgnoreCase)
            .ToList();
        if (nested.Count > 0)
            rows = [.. rows, null, .. nested]; // null: the nested types' heading
        var bottom = y + h - padding / 2;
        int Slots(double top) => Math.Max(0, (int)((bottom - top) / rowPitch));

        // The counts only when some rows are left out: then they tell what the card holds.
        var dividerY = y + headerHeight;
        var firstRowY = dividerY + 4 * u;
        if (rows.Count > Slots(firstRowY))
        {
            var countLines = WrapParts(
                CountParts(node, sections, nested.Count),
                MaxChars(w - 2 * padding, smallSize),
                2
            );
            var countsY = y + headerHeight + 13 * u;
            for (var index = 0; index < countLines.Count; index++)
            {
                svg.Append(
                    string.Create(
                        CultureInfo.InvariantCulture,
                        $"""<text x="{x + padding:0.##}" y="{countsY + index * 11 * u:0.##}" class="summaryMuted" font-size="{smallSize:0.##}px" {textOpacity}>{Encode(countLines[index])}</text>"""
                    )
                );
            }
            dividerY = countsY + (countLines.Count - 1) * 11 * u + 7 * u;
            firstRowY = dividerY + 4 * u;
            svg.Append(
                string.Create(
                    CultureInfo.InvariantCulture,
                    $"""<line x1="{x:0.##}" y1="{dividerY:0.##}" x2="{x + w:0.##}" y2="{dividerY:0.##}" stroke="{DColors.CardDivider}" stroke-width="{u:0.##}"/>"""
                )
            );
        }

        var slots = Slots(firstRowY);
        var shown = rows.Count <= slots ? rows.Count : Math.Max(0, slots - 1);
        if (shown > 0 && rows[shown - 1] is null)
            shown--; // No heading without a row under it
        var rowChars = MaxChars(w - 2 * padding - 13 * u, rowSize);
        for (var index = 0; index < shown; index++)
        {
            var rowY = firstRowY + index * rowPitch;
            if (rows[index] is not { } member)
            {
                svg.Append(
                    string.Create(
                        CultureInfo.InvariantCulture,
                        $"""<text x="{x + padding:0.##}" y="{rowY + rowPitch / 2:0.##}" class="summaryMuted" dominant-baseline="middle" font-size="{smallSize
                            * 0.85:0.##}px" letter-spacing="{smallSize * 0.07:0.##}px" {textOpacity}>{(node.Type.IsType ? "NESTED TYPES" : "CHILDREN")}</text>"""
                    )
                );
                continue;
            }
            var isPrivate = member.IsPrivate ?? false;
            var cssClass = isPrivate ? "summaryMember summaryPrivate" : "summaryMember";
            svg.Append(
                string.Create(
                    CultureInfo.InvariantCulture,
                    $"""
                    <use href="#{NodeSvg.IconName(member)}" x="{x + padding:0.##}" y="{rowY + 2 * u:0.##}" width="{9 * u:0.##}" height="{9
                        * u:0.##}" {nodeOpacity}/>
                    <text x="{x + padding + 13 * u:0.##}" y="{rowY + rowPitch / 2:0.##}" class="{cssClass}" dominant-baseline="middle" font-size="{rowSize:0.##}px" {textOpacity}>{SummaryRowText(member, rowChars)}</text>
                    """
                )
            );
            if (isPrivate)
                svg.Append(BadgeSvg.Private(new Rect(x + padding, rowY + 2 * u, 9 * u, 9 * u), nodeOpacity));
        }

        if (shown < rows.Count && slots > 0)
        {
            var rowY = firstRowY + shown * rowPitch;
            var more = rows.Skip(shown).Count(r => r is not null);
            svg.Append(
                string.Create(
                    CultureInfo.InvariantCulture,
                    $"""<text x="{x + padding + 13 * u:0.##}" y="{rowY + rowPitch / 2:0.##}" class="summaryMuted" dominant-baseline="middle" font-size="{smallSize:0.##}px" {textOpacity}>+ {more} more</text>"""
                )
            );
        }

        var hoverGroup = NodeSvg.BuildHoverGroup(
            elementId,
            "hoverable",
            rect,
            NodeSvg.TitleName(node),
            NodeSvg.LinkHandleSvg(node, new Pos(x + w + NodeSvg.LinkHandleGap, y + h / 2), isEditing),
            isLinePreview: true
        );
        var textZoom = TextZoom(parentZoom);
        svg.Append(hoverGroup);
        svg.Append(NodeSvg.SelectedNodeSvg(node, rect, isEditing));
        svg.Append(BadgeSvg.Problem(problems, rect, textZoom));
        svg.Append(BadgeSvg.Hidden(hiddenChildCount, rect, textZoom));
        return svg.ToString();
    }

    // A summary row: the name, then its signature (muted) in what room is left, like a full row
    // (MemberDetailsSvg).
    static string SummaryRowText(Node member, int maxChars)
    {
        var name = Clip(member.DisplayName, maxChars);
        var signature = ClipRow(member.Signature ?? "", maxChars - name.Length);
        if (signature.Length == 0)
            return Encode(name);
        return string.Create(
            CultureInfo.InvariantCulture,
            $"""{Encode(name)}<tspan class="memberSignature">{Encode(signature)}</tspan>"""
        );
    }

    // A card's box and header: the border in the node's color, the header tinted with its
    // background, the icon, the name and "class · Namespace". u scales the layout (1 for a
    // 180-unit card), textU the text (u, or capped like the other names).
    static string CardFrameSvg(Node node, Rect rect, double u, double textU)
    {
        var (x, y, w, h) = (rect.X, rect.Y, rect.Width, rect.Height);
        var (border, background) = CardColors(node);
        var (nodeOpacity, textOpacity) = NodeSvg.HiddenAttributes(node);
        var headerHeight = MemberCard.CardHeaderHeight * u;
        var radius = Math.Min(6 * u, Math.Min(w, h) / 4);
        var strokeWidth = node.IsEditMode ? 10 : 1.2 * Math.Min(u, 1.5);
        var padding = 9 * u;
        var iconSize = 22 * Math.Min(u, textU * 1.5);
        var nameSize = 12 * textU;
        var smallSize = 8.5 * textU;
        var textLeft = x + padding + iconSize + 4 * u;
        // The name and the kind line as one block, centered in the header.
        var nameTop = y + (headerHeight - (nameSize + 3 * textU + smallSize)) / 2;
        var kindTop = nameTop + nameSize + 3 * textU;
        var nameChars = MaxChars(x + w - padding - textLeft, nameSize);
        var kindText = node.Parent is { IsRoot: false } parent
            ? $"{KindText(node)} · {parent.DisplayName}"
            : KindText(node);
        return string.Create(
            CultureInfo.InvariantCulture,
            $"""
            <g {nodeOpacity}>
            <rect x="{x:0.##}" y="{y:0.##}" width="{w:0.##}" height="{h:0.##}" rx="{radius:0.##}" fill="{DColors.CardBackground}" stroke="{border}" stroke-width="{strokeWidth:0.##}"/>
            <path d="M{x:0.##},{y + headerHeight:0.##} V{y + radius:0.##} a{radius:0.##},{radius:0.##} 0 0 1 {radius:0.##},{-radius:0.##} H{x
                + w
                - radius:0.##} a{radius:0.##},{radius:0.##} 0 0 1 {radius:0.##},{radius:0.##} V{y + headerHeight:0.##} Z" fill="{background}" stroke="{border}" stroke-width="{strokeWidth:0.##}"/>
            <use href="#{NodeSvg.IconName(node)}" x="{x + padding:0.##}" y="{y + (headerHeight - iconSize) / 2:0.##}" width="{iconSize:0.##}" height="{iconSize:0.##}"/>
            </g>
            <text x="{textLeft:0.##}" y="{nameTop:0.##}" class="nodeName" dominant-baseline="hanging" font-size="{nameSize:0.##}px" font-weight="600" {textOpacity}>{Encode(Clip(
                node.DisplayName,
                nameChars
            ))}</text>
            <text x="{textLeft:0.##}" y="{kindTop:0.##}" class="summaryMuted" dominant-baseline="hanging" font-size="{smallSize:0.##}px" {textOpacity}>{Encode(Clip(
                kindText,
                MaxChars(x + w - padding - textLeft, smallSize)
            ))}</text>
            """
        );
    }

    // A card's border and header tint: the same lavender on every type (DColors.CardFrame,
    // CardHeader), unless the user set the node's color, or it is being edited.
    internal static (string Border, string Background) CardColors(Node node) =>
        NodeSvg.HasOwnColors(node) ? NodeSvg.NodeColors(node) : DefaultCardColors;

    internal static (string Border, string Background) DefaultCardColors => (DColors.CardFrame, DColors.CardHeader);

    // An open card node (NodeViewPolicy.IsMemberCardNode): its box is the card, with the header at
    // the top and the children (member rows, nested types) under it, in its child coordinates
    // from the box's top left (NodeLayout.ArrangeCardNode). Replaces the container box and the
    // name under it.
    public static string GetCardNodeSvg(
        Node node,
        Rect nodeCanvasRect,
        double parentZoom,
        double childrenZoom,
        string childrenContent,
        bool isEditing = false,
        NodeProblems? problems = null,
        int hiddenChildCount = 0
    )
    {
        var elementId = PointerId.FromNode(node.Id).ElementId;
        var hoverClass = node.IsEditMode ? "hoverableedit" : "hoverable";
        var (w, h) = (nodeCanvasRect.Width, nodeCanvasRect.Height);
        var innerRect = new Rect(0, 0, w, h);
        var hoverGroup = NodeSvg.BuildHoverGroup(
            elementId,
            hoverClass,
            innerRect,
            NodeSvg.TitleName(node),
            isOpen: true,
            headerHeight: MemberCard.CardHeaderHeight * childrenZoom
        );
        var frame = CardFrameSvg(node, innerRect, childrenZoom, TextZoom(childrenZoom));
        var textZoom = TextZoom(parentZoom);
        return string.Create(
            CultureInfo.InvariantCulture,
            $"""
            <svg x="{nodeCanvasRect.X:0.##}" y="{nodeCanvasRect.Y:0.##}" width="{w:0.##}" height="{h:0.##}" viewBox="0 0 {w:0.##} {h:0.##}" xmlns="http://www.w3.org/2000/svg">
              {frame}
              {hoverGroup}
              {childrenContent}
            </svg>
            {NodeSvg.SelectedNodeSvg(node, nodeCanvasRect, isEditing)}
            {BadgeSvg.Problem(problems, nodeCanvasRect, textZoom)}
            {BadgeSvg.Hidden(hiddenChildCount, nodeCanvasRect, textZoom)}
            """
        );
    }

    // "class", "interface", … for a type; the node type's plain name otherwise.
    static string KindText(Node node) =>
        node.Type switch
        {
            NodeType.ClassType or NodeType.Type => "class",
            NodeType.InterfaceType => "interface",
            NodeType.EnumType => "enum",
            NodeType.StructType => "struct",
            NodeType.RecordType => "record",
            NodeType.Resource => "resource",
            NodeType.Module => "module",
            _ => node.Type.ToString().ToLowerInvariant(),
        };

    // ["3 fields", "1 constructor", "9 methods", "2 nested types"]
    static List<string> CountParts(Node node, IReadOnlyList<MemberCard.Section> sections, int nested)
    {
        var parts = sections
            .Select(s => Count(s.Rows.Count, MemberCard.SectionTitle(s.Rows[0].Type).ToLowerInvariant()))
            .ToList();
        if (nested > 0)
            parts.Add(Count(nested, node.Type.IsType ? "nested types" : "children"));
        return parts;
    }

    // The parts joined with " · " into at most maxLines lines of maxChars, the last one clipped.
    static List<string> WrapParts(List<string> parts, int maxChars, int maxLines)
    {
        var lines = new List<string>();
        var current = "";
        foreach (var part in parts)
        {
            var candidate = current.Length == 0 ? part : $"{current} · {part}";
            if (candidate.Length <= maxChars || current.Length == 0 || lines.Count == maxLines - 1)
            {
                current = candidate;
                continue;
            }
            lines.Add(current);
            current = part;
        }
        if (current.Length > 0)
            lines.Add(Clip(current, maxChars));
        return lines;
    }

    // "1 method", "2 methods" from the plural; "properties" → "property".
    static string Count(int count, string plural)
    {
        if (count != 1)
            return $"{count} {plural}";
        var singular =
            plural.EndsWith("ies", StringComparison.Ordinal) ? plural[..^3] + "y"
            : plural.EndsWith('s') ? plural[..^1]
            : plural;
        if (plural == "children")
            singular = "child";
        if (plural == "nested types")
            singular = "nested type";
        return $"1 {singular}";
    }
}
