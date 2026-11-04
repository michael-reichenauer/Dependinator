using System.Globalization;
using System.Web;
using Dependinator.UI.Diagrams.Interaction;
using Dependinator.UI.Modeling;
using Dependinator.UI.Modeling.Models;
using Dependinator.UI.Shared.Types;
using NodeType = Dependinator.Core.Parsing.NodeType;

namespace Dependinator.UI.Diagrams.Svg;

// The member card's drawing: behind an open container's member rows, a card with a heading per
// section (see MemberCard); and, while the rows are still too small to read, a summary card in
// place of the container (see NodeViewPolicy.IsMemberSummary).
static partial class NodeSvg
{
    const double HeadingFontSize = 6.5; // In member units, like the 8-unit member names
    const double CardCornerRadius = 4;
    const double CardStrokeWidth = 0.6;

    // The card and its section headings, in the container's children canvas space.
    public static string GetMemberCardSvg(
        Node node,
        Pos childrenOffset,
        double childrenZoom,
        Func<Node, bool>? isRowShown = null
    )
    {
        var card = MemberCard.Bounds(node, isRowShown);
        if (card == Rect.None)
            return "";

        double X(double x) => childrenOffset.X + x * childrenZoom;
        double Y(double y) => childrenOffset.Y + y * childrenZoom;
        var textZoom = TextZoom(childrenZoom);
        var fontSize = HeadingFontSize * textZoom;
        var (nodeOpacity, textOpacity) = HiddenAttributes(node);

        var svg = new System.Text.StringBuilder();
        svg.Append(
            string.Create(
                CultureInfo.InvariantCulture,
                $"""<rect class="memberCard" x="{X(card.X):0.##}" y="{Y(card.Y):0.##}" width="{card.Width * childrenZoom:0.##}" height="{card.Height
                    * childrenZoom:0.##}" rx="{CardCornerRadius * childrenZoom:0.##}" fill="{DColors.CardBackground}" stroke="{DColors.CardBorder}" stroke-width="{CardStrokeWidth
                    * textZoom:0.##}" pointer-events="none" {nodeOpacity}/>"""
            )
        );

        foreach (var heading in MemberCard.Headings(node, isRowShown))
        {
            if (!heading.IsColumnTop)
            {
                svg.Append(
                    string.Create(
                        CultureInfo.InvariantCulture,
                        $"""<line x1="{X(heading.X - MemberCard.Padding):0.##}" y1="{Y(heading.Y):0.##}" x2="{X(heading.X
                            + heading.Width
                            + MemberCard.Padding):0.##}" y2="{Y(heading.Y):0.##}" stroke="{DColors.CardDivider}" stroke-width="{CardStrokeWidth
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
        var rect = MemberCard.PrivateToggleRect(node, isRowShown);
        if (rect == Rect.None)
            return "";
        var count = MemberCard.PrivateCount(node);
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
        // Sizes for a 180 px card: it shows from about 200 px (NodeViewPolicy), and its text stops
        // growing soon after, near the size the rows have when the full card replaces it, so
        // growing it reveals more rows instead.
        var u = Math.Min(rect.Width / 180, 1.3);
        var (x, y, w, h) = (rect.X, rect.Y, rect.Width, rect.Height);
        var (border, background) = NodeColors(node);
        var (nodeOpacity, textOpacity) = HiddenAttributes(node);
        var elementId = PointerId.FromNode(node.Id).ElementId;

        var headerHeight = 36 * u;
        var nameSize = 12 * u;
        var smallSize = 8.5 * u;
        var rowSize = 9 * u;
        var rowPitch = 13 * u;
        var padding = 9 * u;
        var textLeft = x + padding + 26 * u;

        var svg = new System.Text.StringBuilder();
        svg.Append(
            string.Create(
                CultureInfo.InvariantCulture,
                $"""
                <g {nodeOpacity}>
                <rect x="{x:0.##}" y="{y:0.##}" width="{w:0.##}" height="{h:0.##}" rx="{6 * u:0.##}" fill="{DColors.CardBackground}" stroke="{border}" stroke-width="{1.2
                    * u:0.##}"/>
                <path d="M{x:0.##},{y + headerHeight:0.##} V{y + 6 * u:0.##} a{6 * u:0.##},{6 * u:0.##} 0 0 1 {6 * u:0.##},{-6 * u:0.##} H{x
                    + w
                    - 6 * u:0.##} a{6 * u:0.##},{6 * u:0.##} 0 0 1 {6 * u:0.##},{6 * u:0.##} V{y + headerHeight:0.##} Z" fill="{background}" stroke="{border}" stroke-width="{1.2
                    * u:0.##}"/>
                <use href="#{IconName(node)}" x="{x + padding:0.##}" y="{y + 7 * u:0.##}" width="{22 * u:0.##}" height="{22
                    * u:0.##}"/>
                </g>
                """
            )
        );

        var nameChars = MaxChars(x + w - padding - textLeft, nameSize);
        var kindText = node.Parent is { IsRoot: false } parent
            ? $"{KindText(node)} · {parent.DisplayName}"
            : KindText(node);
        svg.Append(
            string.Create(
                CultureInfo.InvariantCulture,
                $"""
                <text x="{textLeft:0.##}" y="{y + 7 * u:0.##}" class="nodeName" dominant-baseline="hanging" font-size="{nameSize:0.##}px" font-weight="600" {textOpacity}>{Encode(Clip(
                    node.DisplayName,
                    nameChars
                ))}</text>
                <text x="{textLeft:0.##}" y="{y + 29 * u:0.##}" class="summaryMuted" font-size="{smallSize:0.##}px" {textOpacity}>{Encode(Clip(
                    kindText,
                    MaxChars(x + w - padding - textLeft, smallSize)
                ))}</text>
                """
            )
        );

        var members = node.Children.Where(c => c.Type.IsMember && (isRowShown?.Invoke(c) ?? true)).ToList();
        var sections = MemberCard.Sections(members, (a, b) => string.CompareOrdinal(a.Name, b.Name));
        // The counts, on up to two lines.
        var countLines = WrapParts(CountParts(node, sections), MaxChars(w - 2 * padding, smallSize), 2);
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
        countsY += (countLines.Count - 1) * 11 * u;

        var dividerY = countsY + 7 * u;
        svg.Append(
            string.Create(
                CultureInfo.InvariantCulture,
                $"""<line x1="{x:0.##}" y1="{dividerY:0.##}" x2="{x + w:0.##}" y2="{dividerY:0.##}" stroke="{DColors.CardDivider}" stroke-width="{u:0.##}"/>"""
            )
        );

        // Public members first (in section order), then private ones, as many as fit.
        var ordered = sections.SelectMany(s => s.Rows).OrderBy(m => m.IsPrivate ?? false).ToList();
        var firstRowY = dividerY + 4 * u;
        var slots = Math.Max(0, (int)((y + h - padding / 2 - firstRowY) / rowPitch));
        var shown = ordered.Count <= slots ? ordered.Count : Math.Max(0, slots - 1);
        var rowChars = MaxChars(w - 2 * padding - 13 * u, rowSize);
        for (var index = 0; index < shown; index++)
        {
            var member = ordered[index];
            var rowY = firstRowY + index * rowPitch;
            var cssClass = member.IsPrivate ?? false ? "summaryMember summaryPrivate" : "summaryMember";
            svg.Append(
                string.Create(
                    CultureInfo.InvariantCulture,
                    $"""
                    <use href="#{IconName(member)}" x="{x + padding:0.##}" y="{rowY + 2 * u:0.##}" width="{9 * u:0.##}" height="{9
                        * u:0.##}" {nodeOpacity}/>
                    <text x="{x + padding + 13 * u:0.##}" y="{rowY + rowPitch / 2:0.##}" class="{cssClass}" dominant-baseline="middle" font-size="{rowSize:0.##}px" {textOpacity}>{Encode(Clip(
                        member.DisplayName,
                        rowChars
                    ))}</text>
                    """
                )
            );
        }

        if (shown < ordered.Count && slots > 0)
        {
            var rowY = firstRowY + shown * rowPitch;
            svg.Append(
                string.Create(
                    CultureInfo.InvariantCulture,
                    $"""<text x="{x + padding + 13 * u:0.##}" y="{rowY + rowPitch / 2:0.##}" class="summaryMuted" dominant-baseline="middle" font-size="{smallSize:0.##}px" {textOpacity}>+ {ordered.Count
                        - shown} more</text>"""
                )
            );
        }

        var hoverGroup = BuildHoverGroup(
            elementId,
            "hoverable",
            rect,
            TitleName(node),
            node.HtmlDescription,
            LinkHandleSvg(node, new Pos(x + w + LinkHandleGap, y + h / 2), isEditing),
            isLinePreview: true
        );
        var textZoom = TextZoom(parentZoom);
        svg.Append(hoverGroup);
        svg.Append(SelectedNodeSvg(node, rect, isEditing));
        svg.Append(ProblemBadgeSvg(problems, rect, textZoom));
        svg.Append(HiddenBadgeSvg(hiddenChildCount, rect, textZoom));
        return svg.ToString();
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
    static List<string> CountParts(Node node, IReadOnlyList<MemberCard.Section> sections)
    {
        var parts = sections
            .Select(s => Count(s.Rows.Count, MemberCard.SectionTitle(s.Rows[0].Type).ToLowerInvariant()))
            .ToList();
        var nested = node.Children.Count(c => !c.Type.IsMember && !NodeViewPolicy.IsAnnotation(c));
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

    static int MaxChars(double width, double fontSize) =>
        fontSize <= 0 ? 0 : Math.Max(1, (int)(width / (fontSize * AverageCharWidthFactor)));

    static string Clip(string text, int maxChars) =>
        text.Length <= maxChars ? text : text[..Math.Max(1, maxChars - 1)].TrimEnd() + "…";

    static string Encode(string text) => HttpUtility.HtmlEncode(text);
}
