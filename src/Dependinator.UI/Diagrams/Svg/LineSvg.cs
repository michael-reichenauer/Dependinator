using System.Globalization;
using System.Text;
using Dependinator.UI.Diagrams.Interaction;
using Dependinator.UI.Diagrams.Lines;
using Dependinator.UI.Modeling.Models;
using Dependinator.UI.Shared.Types;

namespace Dependinator.UI.Diagrams.Svg;

static class LineSvg
{
    // The filled circle marking the line's start point, slightly larger than the stroke.
    const double StartCircleExtraRadius = 1.5;

    // Extra width of the invisible hover/hit polyline so thin lines are easy to hover and click.
    const double HitTargetExtraWidth = 10;

    // Classes of the hit polyline, which the .hoverable:hover rule in Canvas.razor skips, and of
    // the polyline that draws the hover highlight instead.
    internal const string HitClass = "line-hit";
    internal const string HoverClass = "line-hover";

    // Selection rendering: highlight stroke widening, endpoint circle enlargement, and the
    // visible/touch radii of the segment-point handles (relative to the endpoint circle).
    const double SelectedStrokeExtraWidth = 5;
    const double SelectedCircleExtraRadius = 3;
    const double HandleExtraRadius = 1;
    const double HandleTouchExtraRadius = 8;

    // Length of the hollow inheritance arrow head in marker units; markers scale with stroke
    // width, so the on-screen length is this value times the line's stroke width (must match
    // the "arrow-inheritance" marker geometry in Canvas.razor/SvgExportDocument).
    const double InheritanceMarkerLength = 14.5;

    // A path line is drawn at least this wide so the chain reads even among thin lines.
    const double PathMinStrokeWidth = 2;

    // The line left visible behind a stub's hollow inheritance arrow head.
    const double StubInheritanceLineLength = 6;

    // The ring at a stub's far end (see LineStub).
    internal const string StubRingClass = "line-stub-ring";

    // Every aggregated line is drawn at this one width; a line only shows its link-count width
    // (Line.WeightedStrokeWidth) while it is emphasized and Settings › Line widths by link count is on
    // (see LineEmphasis.IsLineWeighted), since the counts are only readable when comparing the few
    // lines of a selection.
    const double PlainStrokeWidth = 1;

    public static string GetLineSvg(
        Line line,
        Pos nodeCanvasPos,
        double childrenZoom,
        bool isDimmed = false,
        bool isCyclic = false,
        bool isPath = false,
        bool isViolation = false,
        bool isWeighted = false,
        bool includeLabel = true,
        bool isLit = false,
        bool isEditing = false
    )
    {
        if (!LinePathGeometry.TryGetLocalEndpoints(line, out var localEndpoints))
            return "";

        var endpoints = LinePathGeometry.ToRendered(localEndpoints, nodeCanvasPos, childrenZoom);
        var polylinePoints = LinePathGeometry.GetRenderedPolylinePoints(line, nodeCanvasPos, childrenZoom);
        var elementId = PointerId.FromLine(line.Id).ElementId;

        return BuildLineSvg(
            line,
            endpoints,
            polylinePoints,
            elementId,
            childrenZoom,
            isDimmed,
            isCyclic,
            isPath,
            isViolation,
            isWeighted,
            includeLabel,
            isLit,
            isEditing
        );
    }

    // A line's label on its own, for the label layer NodeTreeSvg draws above the child nodes
    // (the sibling lines themselves lie under the nodes, which would cover a label drawn with
    // them). Dimmed, softened and hover-previewed like its line (the same classes and data-n);
    // hovering the line does not restore it, which is fine.
    public static string GetLineLabelSvg(
        Line line,
        Pos nodeCanvasPos,
        double childrenZoom,
        bool isDimmed,
        bool isLit = false
    )
    {
        if (!line.HasLabel || !LinePathGeometry.TryGetLocalEndpoints(line, out _))
            return "";

        var polylinePoints = LinePathGeometry.GetRenderedPolylinePoints(line, nodeCanvasPos, childrenZoom);
        var (text, hit) = LabelSvg(line, polylinePoints, childrenZoom);
        var labelClass = "line-vis line-text" + (isDimmed ? " line-dim" : "") + (isLit ? " line-lit" : "");
        return $"""<g class="{labelClass}" data-n="{line.Source.Id.Value} {line.Target.Id.Value}">{text}</g>{hit}""";
    }

    public static string GetDirectLineSvg(
        Line line,
        Node ancestor,
        Pos nodeCanvasPos,
        double childrenZoom,
        bool isDimmed = false,
        bool isPath = false,
        bool isViolation = false,
        bool isWeighted = false,
        bool isLit = false,
        bool isEditing = false
    )
    {
        if (line.RenderAncestor != ancestor)
            return "";

        return GetLineSvg(
            line,
            nodeCanvasPos,
            childrenZoom,
            isDimmed,
            isPath: isPath,
            isViolation: isViolation,
            isWeighted: isWeighted,
            isLit: isLit,
            isEditing: isEditing
        );
    }

    static string BuildLineSvg(
        Line line,
        LinePathGeometry.LineEndpoints endpoints,
        IReadOnlyList<Pos> polylinePoints,
        string elementId,
        double childrenZoom,
        bool isDimmed,
        bool isCyclic,
        bool isPath,
        bool isViolation,
        bool isWeighted,
        bool includeLabel,
        bool isLit,
        bool isEditing
    )
    {
        // Explorer (focused) lines share the direct line's accent color and arrow. Dash patterns
        // tell the on-demand lines apart from each other and from the solid aggregated lines:
        // the explorer's pinned pair lines are dashed, its transient focus lines dotted. A line
        // on the dependency path the user asked for (View › Find Path) outranks all of that.
        // A hidden node's lines are dash-dotted as well as faint: faint alone is what a line
        // unrelated to the selection looks like (the line-dim CSS in Canvas.razor), so the
        // pattern is what tells "goes to a hidden node" apart from "not what you selected".
        var isAccent = line.IsDirect || line.IsFocused;
        var color =
            isPath ? DColors.PathLine
            : isViolation ? DColors.RuleLine
            : isAccent ? DColors.DirectLine
            : isCyclic ? DColors.CycleLine
            : !line.IsVisible ? DColors.LineHidden
            : line.IsCousin ? DColors.CousinLine
            : DColors.Line;

        // The hollow inheritance arrow head is only drawn where the line enters the real
        // inheritance target (the supertype); hidden/accent/cycle/path styling takes precedence.
        var isInheritanceHead =
            !isPath && !isViolation && !isAccent && !isCyclic && line.IsVisible && line.HasInheritanceTargetEnd;

        var markerId =
            isPath ? "arrow-path"
            : isViolation ? "arrow-rule"
            : isAccent ? "arrow-direct"
            : isCyclic ? "arrow-cycle"
            : !line.IsVisible ? "arrow-hidden"
            : isInheritanceHead ? "arrow-inheritance"
            : line.IsCousin ? "arrow-cousin"
            : "arrow-line";

        // Direct lines keep their fixed accent width; other lines are thin unless emphasized.
        var baseWidth = line.IsDirect || isWeighted ? line.WeightedStrokeWidth : PlainStrokeWidth;
        var strokeWidth = isPath || isViolation ? Math.Max(baseWidth, PathMinStrokeWidth) : baseWidth;
        var circleRadius = strokeWidth + StartCircleExtraRadius;
        var dashArray =
            line.IsDirect ? " stroke-dasharray=\"6,6\""
            : line.IsFocused ? " stroke-dasharray=\"2,4\""
            : !line.IsVisible ? " stroke-dasharray=\"6,4,1,4\""
            : "";

        // A line between a container's edge and one of its children is drawn as a stub at the
        // child (see LineStub): the same line with its id, classes and tooltip, only short, with
        // a ring at the far end in place of the start circle there.
        var isStub = LineStub.IsStub(line, isPath);
        IReadOnlyList<Pos> hitPolylinePoints = polylinePoints;
        var ringSvg = "";
        var hasStartCircle = true;
        if (isStub)
        {
            var isIncoming = LineStub.IsIncoming(line);
            var anchor = isIncoming ? polylinePoints[^1] : polylinePoints[0];
            var minLength = isInheritanceHead ? InheritanceMarkerLength * strokeWidth + StubInheritanceLineLength : 0;
            var stub = LineStub.GetGeometry(line, anchor, childrenZoom, minLength);
            polylinePoints = [stub.Start, stub.End];
            hitPolylinePoints = [stub.HitStart, stub.HitEnd];
            endpoints = new LinePathGeometry.LineEndpoints(stub.Start.X, stub.Start.Y, stub.End.X, stub.End.Y);
            hasStartCircle = !isIncoming;
            ringSvg = string.Create(
                CultureInfo.InvariantCulture,
                $"""<circle class="{StubRingClass}" cx="{stub.Ring.X:0.##}" cy="{stub.Ring.Y:0.##}" r="{stub.RingRadius:0.##}" fill="none" stroke="{color}" stroke-width="{strokeWidth:0.##}" />"""
            );
        }

        // The hollow marker starts at the polyline end (refX=0) and extends forward, so the
        // visible line is pulled back by the arrow-head length to keep the tip on the node edge
        // and the stroke out of the hollow triangle. Hit/selection paths keep the full length.
        var visiblePoints = isInheritanceHead
            ? TrimEndForMarker(polylinePoints, InheritanceMarkerLength * strokeWidth)
            : polylinePoints;
        var points = ToPolylinePoints(visiblePoints);
        var hitPoints = ToPolylinePoints(hitPolylinePoints);
        var selectedSvg = SelectedLineSvg(
            line,
            polylinePoints,
            strokeWidth,
            hasHandles: !isStub,
            isEditing,
            childrenZoom
        );
        var startCircleSvg = hasStartCircle
            ? string.Create(
                CultureInfo.InvariantCulture,
                $"""<circle cx="{endpoints.X1:0.##}" cy="{endpoints.Y1:0.##}" r="{circleRadius:0.##}" fill="{color}" />"""
            )
            : "";
        var (labelSvg, labelHitSvg) = includeLabel ? LabelSvg(line, polylinePoints, childrenZoom) : ("", "");

        // The hover group's data-name, "Source→Target (n)" with the long names, is what the e2e
        // locators match; the tooltip itself is the hover card (TooltipService, initTooltips in jsInterop.js).
        var name = $"{line.Source.HtmlLongName}→{line.Target.HtmlLongName} ({line.Links.Count})";

        // The second, fully transparent polyline is the hover/hit target: it traces the same
        // path but much wider, so hovering/clicking near the thin visible line still hits it.
        // The .hoverable:hover rule skips it (HitClass), so hovering does not shrink it: WebKit
        // hit-tests a press right after the hover restyle against the shrunken stroke and
        // misses the line. The third polyline, never a target and unstroked until hovered,
        // draws the hover highlight along the visible (trimmed) path, so it stays out of a
        // hollow arrow head (its caps and joins come from the HoverClass rule).
        // The outer group lets a hover over the hit target restore a dimmed line (see the
        // line-dim CSS in Canvas.razor).
        // Soft lines (Canvas.razor) draw resting lines faint and keep the classes below at full
        // strength: line-lit for the selection's own lines, line-accent for explorer and direct
        // lines. data-n names the line's two end nodes, for the hover preview (linePreview.js),
        // which lights a hovered node's lines without a rerender.
        // A call between two members of the same type is shown only with one of them selected
        // or hovered (line-inner in Canvas.razor and jsInterop.js): at rest a type's card would
        // be crossed by all of its internal calls. Lines that mean something stay.
        var isInner =
            IsInnerMemberLine(line)
            && !isAccent
            && !isCyclic
            && !isPath
            && !isViolation
            && !line.IsSelected
            && !line.HasLabel;
        var hitPointerEvents = isInner && !isLit ? " pointer-events=\"none\"" : "";
        var visibleClass =
            (isDimmed ? "line-vis line-dim" : "line-vis")
            + (isInner ? " line-inner" : "")
            + (isLit ? " line-lit" : "")
            + (isAccent ? " line-accent" : "")
            + (isCyclic ? " line-cycle" : "")
            + (isPath ? " line-path" : "")
            + (isViolation ? " line-rule" : "");
        return string.Create(
            CultureInfo.InvariantCulture,
            $"""
            <g class="line">
            <g class="{visibleClass}" data-n="{line.Source.Id.Value} {line.Target.Id.Value}">
            <polyline points="{points}" fill="none" stroke-width="{strokeWidth:0.##}" stroke="{color}" stroke-linecap="round" stroke-linejoin="round" marker-end="url(#{markerId})"{dashArray} />
            {startCircleSvg}
            {ringSvg}
            {labelSvg}
            </g>
            <g class="hoverable" id="{elementId}" data-name="{name}">
              <polyline id="{elementId}" class="{HitClass}" points="{hitPoints}" fill="none" stroke-width="{strokeWidth
                + HitTargetExtraWidth:0.##}" stroke="black" stroke-opacity="0" stroke-linecap="round" stroke-linejoin="round"{hitPointerEvents} />
              <polyline class="{HoverClass}" points="{points}" fill="none" pointer-events="none" />
            </g>
            {labelHitSvg}
            {selectedSvg}
            </g>
            """
        );
    }

    // Between two member rows of the same container (a call or use inside one type).
    internal static bool IsInnerMemberLine(Line line) =>
        line.Source.Type.IsMember && line.Target.Type.IsMember && line.Source.Parent == line.Target.Parent;

    // The user's label on the line (at its middle unless dragged: Line.LabelPosition), with its
    // description under it in a smaller italic. The texts are quieter than node names (a label
    // annotates a line, it is not a node): smaller, and they stop growing with the zoom earlier.
    // What is drawn is limited, one clipped row for the label and a few wrapped rows for the
    // description (the user's own line breaks kept); the line's hover card shows them in full (up to 20 description rows). The
    // lineLabel/lineDescription classes draw a background-colored halo (paint-order) so the text
    // stays legible over the line and whatever it crosses.
    internal const double LabelFontSize = 6.5;
    internal const double LabelDescriptionFontSize = 5.5;
    internal const double LabelMaxTextZoom = 2.2;
    internal const int LabelMaxChars = 32;
    internal const int LabelDescriptionMaxChars = 38;
    internal const int LabelDescriptionMaxLines = 3;

    // Class of a label's hit rect (see LabelHitSvg).
    internal const string LabelHitClass = "line-label-hit";

    // Class of a bend-point handle's hit circle copied above a label (see LabelHitSvg).
    internal const string LabelHandleClass = "line-label-handle";

    // Offset (in em) from a row's middle down to its alphabetic baseline: the description's rows
    // are <tspan>s, which WebKit and Firefox do not center with dominant-baseline.
    const double MiddleToBaselineFactor = 0.35;

    // Where a label's texts go: the point on the line, the font sizes, the texts as drawn (the
    // label clipped, the description wrapped) and the box around them, from the rough text widths.
    readonly record struct LabelLayout(
        Pos At,
        double FontSize,
        double DescriptionFontSize,
        double RowHeight,
        string? LabelText,
        IReadOnlyList<string> Rows,
        double LabelY,
        double FirstRowY,
        Rect Box
    )
    {
        // The label's hit rect (see LabelHitSvg): the box with a little room around the texts.
        public Rect HitBox
        {
            get
            {
                var padding = FontSize * 0.3;
                return new Rect(Box.X - padding, Box.Y - padding, Box.Width + 2 * padding, Box.Height + 2 * padding);
            }
        }
    }

    static LabelLayout GetLabelLayout(Line line, IReadOnlyList<Pos> polylinePoints, double childrenZoom)
    {
        var at = LinePathGeometry.PointAt(polylinePoints, line.LabelPosition);
        var textZoom = Math.Min(childrenZoom, LabelMaxTextZoom);
        var fontSize = LabelFontSize * textZoom;
        var descriptionFontSize = LabelDescriptionFontSize * textZoom;
        var rowHeight = descriptionFontSize * SvgText.DescriptionLineHeightFactor;

        var labelText = line.Label is null ? null : SvgText.Clip(line.Label, LabelMaxChars);
        var rows = SvgText.GetUserTextLines(line.LabelDescription, LabelDescriptionMaxChars, LabelDescriptionMaxLines);

        // With both texts the line runs between them: the label just above, the description's
        // rows below. A single text is centered on the line itself.
        var hasBoth = labelText is not null && rows.Count > 0;
        var labelY = hasBoth ? at.Y - fontSize * 0.6 : at.Y;
        var firstRowY = hasBoth ? at.Y + descriptionFontSize * 0.75 : at.Y - (rows.Count - 1) * rowHeight / 2;

        var width = Math.Max(
            SvgText.EstimateTextWidth(labelText ?? "", fontSize),
            rows.Select(row => SvgText.EstimateTextWidth(row, descriptionFontSize)).DefaultIfEmpty(0).Max()
        );
        var top = labelText is not null ? labelY - fontSize * 0.6 : firstRowY - descriptionFontSize * 0.6;
        var bottom =
            rows.Count > 0
                ? firstRowY + (rows.Count - 1) * rowHeight + descriptionFontSize * 0.6
                : labelY + fontSize * 0.6;
        var box = new Rect(at.X - width / 2, top, width, bottom - top);
        return new LabelLayout(at, fontSize, descriptionFontSize, rowHeight, labelText, rows, labelY, firstRowY, box);
    }

    // The label's texts, and its hit target (see LabelHitSvg); both "" without a label.
    static (string Text, string Hit) LabelSvg(Line line, IReadOnlyList<Pos> polylinePoints, double childrenZoom)
    {
        if (!line.HasLabel)
            return ("", "");

        var layout = GetLabelLayout(line, polylinePoints, childrenZoom);
        var (at, fontSize, descriptionFontSize, rowHeight, labelText, rows, labelY, firstRowY, _) = layout;
        var firstBaselineY = firstRowY + descriptionFontSize * MiddleToBaselineFactor;
        var opacity = !line.IsVisible ? " opacity=\"0.3\"" : "";

        var labelSvg = labelText is null
            ? ""
            : string.Create(
                CultureInfo.InvariantCulture,
                $"""<text class="lineLabel" x="{at.X:0.##}" y="{labelY:0.##}" font-size="{fontSize:0.##}px" text-anchor="middle" dominant-baseline="central"{opacity}>{SvgText.Encode(
                    labelText
                )}</text>"""
            );

        var tspans = string.Concat(
            rows.Select(
                (row, i) =>
                    string.Create(
                        CultureInfo.InvariantCulture,
                        $"""<tspan x="{at.X:0.##}" dy="{(i == 0 ? 0 : rowHeight):0.##}">{SvgText.Encode(row)}</tspan>"""
                    )
            )
        );
        var descriptionSvg =
            rows.Count == 0
                ? ""
                : string.Create(
                    CultureInfo.InvariantCulture,
                    $"""<text class="lineDescription" x="{at.X:0.##}" y="{firstBaselineY:0.##}" font-size="{descriptionFontSize:0.##}px" text-anchor="middle"{opacity}>{tspans}</text>"""
                );

        return (labelSvg + descriptionSvg, LabelHitSvg(line, layout.HitBox, polylinePoints));
    }

    // A transparent rect over the label's texts, so the label is part of its line: hovering it
    // shows the line's card (the group's id is the line's), a click selects the line, a
    // double-click in edit mode edits the label, and an edit-mode drag moves it along the line
    // (the rect's own id, PointerId.LineLabelElementId). The texts themselves take no pointer
    // events. Labels are drawn above the nodes, a selected line's bend-point handles with the
    // line below them, so a handle under the label gets a transparent copy of its hit circle
    // above the rect (same id): the handle stays draggable and the rest of the label clickable.
    static string LabelHitSvg(Line line, Rect hitBox, IReadOnlyList<Pos> polylinePoints)
    {
        var groupId = PointerId.FromLine(line.Id).ElementId;
        var rectId = PointerId.LineLabelElementId(line.Id);
        var rect = string.Create(
            CultureInfo.InvariantCulture,
            $"""<g class="hoverable" id="{groupId}"><rect id="{rectId}" class="{HitClass} {LabelHitClass}" x="{hitBox.X:0.##}" y="{hitBox.Y:0.##}" width="{hitBox.Width:0.##}" height="{hitBox.Height:0.##}" fill="black" fill-opacity="0" /></g>"""
        );
        return rect + HandlesOverLabelSvg(line, hitBox, polylinePoints);
    }

    // Hit circles for a selected line's bend-point handles that reach into the label's rect.
    static string HandlesOverLabelSvg(Line line, Rect hitBox, IReadOnlyList<Pos> polylinePoints)
    {
        if (!line.IsSelected || line.SegmentPoints.Count == 0 || polylinePoints.Count != line.SegmentPoints.Count + 2)
            return "";

        // The handle's own radius depends on how the line is drawn (weighted, on a path), which
        // the label layer does not know: take the widest it can be.
        var strokeWidth = Math.Max(Math.Max(PlainStrokeWidth, PathMinStrokeWidth), line.WeightedStrokeWidth);
        var radius = strokeWidth + SelectedCircleExtraRadius + HandleTouchExtraRadius;
        var circles = new StringBuilder();
        for (var index = 0; index < line.SegmentPoints.Count; index++)
        {
            var p = polylinePoints[index + 1];
            var isUnderLabel =
                p.X >= hitBox.X - radius
                && p.X <= hitBox.X + hitBox.Width + radius
                && p.Y >= hitBox.Y - radius
                && p.Y <= hitBox.Y + hitBox.Height + radius;
            if (!isUnderLabel)
                continue;

            var elementId = PointerId.FromLinePoint(line.Id, index).ElementId;
            circles.Append(
                CultureInfo.InvariantCulture,
                $"""<circle id="{elementId}" class="{LabelHandleClass}" cx="{p.X:0.##}" cy="{p.Y:0.##}" r="{radius:0.##}" fill="black" fill-opacity="0" />"""
            );
        }
        return circles.ToString();
    }

    static string SelectedLineSvg(
        Line line,
        IReadOnlyList<Pos> polylinePoints,
        double strokeWidth,
        bool hasHandles,
        bool isEditing,
        double childrenZoom
    )
    {
        if (!line.IsSelected)
            return "";

        var color = DColors.LineSelected;
        var segmentControlColor = DColors.Selected;
        var circleRadius = strokeWidth + SelectedCircleExtraRadius;
        var points = ToPolylinePoints(polylinePoints);
        var start = polylinePoints.First();
        var end = polylinePoints.Last();
        var handlePoints = hasHandles ? line.SegmentPoints : [];
        var handlesSvg = string.Join(
            "\n",
            handlePoints.Select(
                (point, index) =>
                {
                    var renderedPoint = polylinePoints[index + 1];
                    var elementId = PointerId.FromLinePoint(line.Id, index).ElementId;
                    // Visible handle plus a larger invisible circle as touch/click hit target.
                    return string.Create(
                        CultureInfo.InvariantCulture,
                        $"""
                    <g class="selectpoint">
                      <circle cx="{renderedPoint.X:0.##}" cy="{renderedPoint.Y:0.##}" r="{circleRadius
                        + HandleExtraRadius:0.##}" fill="{segmentControlColor}" />
                      <circle id="{elementId}" cx="{renderedPoint.X:0.##}" cy="{renderedPoint.Y:0.##}" r="{circleRadius
                        + HandleTouchExtraRadius:0.##}" fill="black" fill-opacity="0" />
                    </g>
                    """
                    );
                }
            )
        );

        // The overlay lies above the hit polyline and carries no id, so it lets presses through
        // to the line: a press on a dash would otherwise click "nothing" and deselect the line.
        return string.Create(
            CultureInfo.InvariantCulture,
            $"""
            <polyline points="{points}" fill="none" stroke="{color}" stroke-width="{strokeWidth
                + SelectedStrokeExtraWidth:0.##}" stroke-linecap="round" stroke-linejoin="round" stroke-dasharray="3,50" pointer-events="none"/>
            <circle cx="{start.X:0.##}" cy="{start.Y:0.##}" r="{circleRadius:0.##}" fill="{color}" pointer-events="none" />
            <circle cx="{end.X:0.##}" cy="{end.Y:0.##}" r="{circleRadius:0.##}" fill="{color}" pointer-events="none" />
            {handlesSvg}
            {(hasHandles && isEditing ? SegmentHandlesSvg(line, polylinePoints, circleRadius, childrenZoom) : "")}
            """
        );
    }

    // The hollow handle in the middle of each segment of a selected line in edit mode: dragging
    // it bends the line there (a new bend point, PointerId.FromLineMidPoint). A segment too short
    // to keep it clear of the handles at its ends gets none. The label lies above the line and
    // would take the press, so a handle it covers moves a quarter along, or is left out.
    const double SegmentHandleTouchExtraRadius = 4;

    static string SegmentHandlesSvg(
        Line line,
        IReadOnlyList<Pos> polylinePoints,
        double circleRadius,
        double childrenZoom
    )
    {
        if (polylinePoints.Count != line.SegmentPoints.Count + 2)
            return "";

        var hitRadius = circleRadius + SegmentHandleTouchExtraRadius;
        // From the handle to a segment end, room for both handles' hit circles.
        var clearance = hitRadius + circleRadius + HandleTouchExtraRadius;
        Rect? labelBox = null;
        if (line.HasLabel)
        {
            var box = GetLabelLayout(line, polylinePoints, childrenZoom).HitBox;
            labelBox = new Rect(
                box.X - hitRadius,
                box.Y - hitRadius,
                box.Width + 2 * hitRadius,
                box.Height + 2 * hitRadius
            );
        }

        // A ring filled with the light selection color: it reads as hollow next to the solid bend
        // handles and covers the line under it on any background (the canvas or a container's tint).
        var color = DColors.Selected;
        var handles = new StringBuilder();
        for (var index = 0; index < polylinePoints.Count - 1; index++)
        {
            var (a, b) = (polylinePoints[index], polylinePoints[index + 1]);
            var length = Math.Sqrt((b.X - a.X) * (b.X - a.X) + (b.Y - a.Y) * (b.Y - a.Y));
            foreach (var percent in SegmentHandlePercents)
            {
                var fraction = percent / 100.0;
                if (Math.Min(fraction, 1 - fraction) * length < clearance)
                    continue;
                var p = new Pos(a.X + (b.X - a.X) * fraction, a.Y + (b.Y - a.Y) * fraction);
                if (labelBox is { } box && IsInside(box, p))
                    continue;

                var elementId = PointerId.FromLineMidPoint(line.Id, index, percent).ElementId;
                handles.Append(
                    CultureInfo.InvariantCulture,
                    $"""
                    <g class="selectpoint segment-handle">
                      <circle cx="{p.X:0.##}" cy="{p.Y:0.##}" r="{circleRadius:0.##}" fill="{DColors.LineSelected}" stroke="{color}" stroke-width="1.5" />
                      <circle id="{elementId}" cx="{p.X:0.##}" cy="{p.Y:0.##}" r="{hitRadius:0.##}" fill="black" fill-opacity="0" />
                    </g>
                    """
                );
                break;
            }
        }
        return handles.ToString();
    }

    // Where along a segment its handle may sit, in order of preference.
    static readonly int[] SegmentHandlePercents = [50, 25, 75];

    static bool IsInside(Rect box, Pos p) =>
        p.X >= box.X && p.X <= box.X + box.Width && p.Y >= box.Y && p.Y <= box.Y + box.Height;

    static string ToPolylinePoints(IReadOnlyList<Pos> points) =>
        string.Join(" ", points.Select(p => string.Create(CultureInfo.InvariantCulture, $"{p.X:0.##},{p.Y:0.##}")));

    // Pulls the last point back along the final segment by length (clamped to that segment), so
    // an end marker drawn ahead of the line end (refX=0) has its tip at the original endpoint.
    static IReadOnlyList<Pos> TrimEndForMarker(IReadOnlyList<Pos> points, double length)
    {
        if (points.Count < 2)
            return points;

        var last = points[^1];
        var previous = points[^2];
        var dx = last.X - previous.X;
        var dy = last.Y - previous.Y;
        var segmentLength = Math.Sqrt(dx * dx + dy * dy);
        if (segmentLength == 0)
            return points;

        var factor = Math.Min(length, segmentLength) / segmentLength;
        var trimmed = new Pos(last.X - dx * factor, last.Y - dy * factor);

        return [.. points.Take(points.Count - 1), trimmed];
    }
}
