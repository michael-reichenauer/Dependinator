using System.Globalization;
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
        bool isLit = false
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
            isLit
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
        var text = LabelSvg(line, polylinePoints, childrenZoom);
        var labelClass = "line-vis line-text" + (isDimmed ? " line-dim" : "") + (isLit ? " line-lit" : "");
        return $"""<g class="{labelClass}" data-n="{line.Source.Id.Value} {line.Target.Id.Value}">{text}</g>""";
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
        bool isLit = false
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
            isLit: isLit
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
        bool isLit
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
        var selectedSvg = SelectedLineSvg(line, polylinePoints, strokeWidth, hasHandles: !isStub);
        var startCircleSvg = hasStartCircle
            ? string.Create(
                CultureInfo.InvariantCulture,
                $"""<circle cx="{endpoints.X1:0.##}" cy="{endpoints.Y1:0.##}" r="{circleRadius:0.##}" fill="{color}" />"""
            )
            : "";
        var labelSvg = includeLabel ? LabelSvg(line, polylinePoints, childrenZoom) : "";

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
            {selectedSvg}
            </g>
            """
        );
    }

    // Between two member rows of the same container (a call or use inside one type).
    internal static bool IsInnerMemberLine(Line line) =>
        line.Source.Type.IsMember && line.Target.Type.IsMember && line.Source.Parent == line.Target.Parent;

    // The user's label at the line's middle, with its description under it in a smaller italic.
    // The texts are quieter than node names (a label annotates a line, it is not a node): smaller,
    // and they stop growing with the zoom earlier. What is drawn is limited, one clipped row for
    // the label and a few wrapped rows for the description (the user's own line breaks kept); the
    // line's hover card shows both in full. The lineLabel/lineDescription classes draw a
    // background-colored halo (paint-order) so the text stays legible over the line and whatever
    // it crosses.
    internal const double LabelFontSize = 6.5;
    internal const double LabelDescriptionFontSize = 5.5;
    internal const double LabelMaxTextZoom = 2.2;
    internal const int LabelMaxChars = 32;
    internal const int LabelDescriptionMaxChars = 38;
    internal const int LabelDescriptionMaxLines = 3;

    // Offset (in em) from a row's middle down to its alphabetic baseline: the description's rows
    // are <tspan>s, which WebKit and Firefox do not center with dominant-baseline.
    const double MiddleToBaselineFactor = 0.35;

    static string LabelSvg(Line line, IReadOnlyList<Pos> polylinePoints, double childrenZoom)
    {
        if (!line.HasLabel)
            return "";

        var middle = LinePathGeometry.MidPoint(polylinePoints);
        var textZoom = Math.Min(childrenZoom, LabelMaxTextZoom);
        var fontSize = LabelFontSize * textZoom;
        var descriptionFontSize = LabelDescriptionFontSize * textZoom;
        var rowHeight = descriptionFontSize * SvgText.DescriptionLineHeightFactor;
        var opacity = !line.IsVisible ? " opacity=\"0.3\"" : "";

        var label = line.Label is null ? null : SvgText.Encode(SvgText.Clip(line.Label, LabelMaxChars));
        var rows = SvgText.GetUserTextLines(line.LabelDescription, LabelDescriptionMaxChars, LabelDescriptionMaxLines);

        // With both texts the line runs between them: the label just above, the description's
        // rows below. A single text is centered on the line itself.
        var hasBoth = label is not null && rows.Count > 0;
        var labelY = hasBoth ? middle.Y - fontSize * 0.6 : middle.Y;
        var firstRowY = hasBoth ? middle.Y + descriptionFontSize * 0.75 : middle.Y - (rows.Count - 1) * rowHeight / 2;
        var firstBaselineY = firstRowY + descriptionFontSize * MiddleToBaselineFactor;

        var labelSvg = label is null
            ? ""
            : string.Create(
                CultureInfo.InvariantCulture,
                $"""<text class="lineLabel" x="{middle.X:0.##}" y="{labelY:0.##}" font-size="{fontSize:0.##}px" text-anchor="middle" dominant-baseline="central"{opacity}>{label}</text>"""
            );
        if (rows.Count == 0)
            return labelSvg;

        var tspans = string.Concat(
            rows.Select(
                (row, i) =>
                    string.Create(
                        CultureInfo.InvariantCulture,
                        $"""<tspan x="{middle.X:0.##}" dy="{(i == 0 ? 0 : rowHeight):0.##}">{SvgText.Encode(row)}</tspan>"""
                    )
            )
        );
        var descriptionSvg = string.Create(
            CultureInfo.InvariantCulture,
            $"""<text class="lineDescription" x="{middle.X:0.##}" y="{firstBaselineY:0.##}" font-size="{descriptionFontSize:0.##}px" text-anchor="middle"{opacity}>{tspans}</text>"""
        );
        return labelSvg + descriptionSvg;
    }

    // hasHandles is false for a stub: it is not drawn along the line's points, so it shows none.
    static string SelectedLineSvg(Line line, IReadOnlyList<Pos> polylinePoints, double strokeWidth, bool hasHandles)
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
            """
        );
    }

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
