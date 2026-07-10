using Dependinator.UI.Modeling.Models;
using Dependinator.UI.Shared.Types;
using static System.FormattableString;

namespace Dependinator.UI.Diagrams.Svg;

static class LineSvg
{
    // The filled circle marking the line's start point, slightly larger than the stroke.
    const double StartCircleExtraRadius = 1.5;

    // Extra width of the invisible hover/hit polyline so thin lines are easy to hover and click.
    const double HitTargetExtraWidth = 10;

    // Selection rendering: highlight stroke widening, endpoint circle enlargement, and the
    // visible/touch radii of the segment-point handles (relative to the endpoint circle).
    const double SelectedStrokeExtraWidth = 5;
    const double SelectedCircleExtraRadius = 3;
    const double HandleExtraRadius = 1;
    const double HandleTouchExtraRadius = 8;

    public static string GetLineSvg(Line line, Pos nodeCanvasPos, double parentZoom, double childrenZoom)
    {
        if (!ShouldRender(line, parentZoom, childrenZoom))
            return "";

        return Render(line, nodeCanvasPos, childrenZoom);
    }

    public static string GetDirectLineSvg(Line line, Node ancestor, Pos nodeCanvasPos, double childrenZoom)
    {
        if (line.RenderAncestor != ancestor)
            return "";

        return Render(line, nodeCanvasPos, childrenZoom);
    }

    static bool ShouldRender(Line line, double parentZoom, double childrenZoom)
    {
        if (NodeViewPolicy.IsTooLargeToBeSeen(childrenZoom))
            return false;

        var connectsParentAndChild = line.Target.Parent == line.Source || line.Source.Parent == line.Target;
        if (connectsParentAndChild && NodeViewPolicy.IsTooLargeToBeSeen(parentZoom))
            return false;

        return true;
    }

    static string Render(Line line, Pos nodeCanvasPos, double childrenZoom)
    {
        if (!LinePathGeometry.TryGetLocalEndpoints(line, out var localEndpoints))
            return "";

        var endpoints = LinePathGeometry.ToRendered(localEndpoints, nodeCanvasPos, childrenZoom);
        var polylinePoints = LinePathGeometry.GetRenderedPolylinePoints(line, nodeCanvasPos, childrenZoom);
        var elementId = PointerId.FromLine(line.Id).ElementId;

        return BuildLineSvg(line, endpoints, polylinePoints, elementId);
    }

    static string BuildLineSvg(
        Line line,
        LinePathGeometry.LineEndpoints endpoints,
        IReadOnlyList<Pos> polylinePoints,
        string elementId
    )
    {
        var color =
            line.IsDirect ? DColors.DirectLine
            : line.IsHidden ? DColors.LineHidden
            : DColors.Line;

        var markerId =
            line.IsDirect ? "arrow-direct"
            : line.IsHidden ? "arrow-hidden"
            : "arrow-line";

        var strokeWidth = line.StrokeWidth;
        var circleRadius = strokeWidth + StartCircleExtraRadius;
        var dashArray = line.IsDirect ? " stroke-dasharray=\"6,6\"" : "";
        var points = ToPolylinePoints(polylinePoints);
        var selectedSvg = SelectedLineSvg(line, polylinePoints);

        var title = $"{line.Source.HtmlLongName}→{line.Target.HtmlLongName} ({line.Links.Count})";
        if (!string.IsNullOrWhiteSpace(line.HtmlDescription))
            title = $"{title}\n\n{line.HtmlDescription}";

        // The second, fully transparent polyline is the hover/hit target: it traces the same
        // path but much wider, so hovering/clicking near the thin visible line still hits it.
        return Invariant(
            $"""
            <polyline points="{points}" fill="none" stroke-width="{strokeWidth:0.##}" stroke="{color}" stroke-linecap="round" stroke-linejoin="round" marker-end="url(#{markerId})"{dashArray} />
            <circle cx="{endpoints.X1:0.##}" cy="{endpoints.Y1:0.##}" r="{circleRadius:0.##}" fill="{color}" />
            <g class="hoverable" id="{elementId}">
              <polyline id="{elementId}" points="{points}" fill="none" stroke-width="{strokeWidth
                + HitTargetExtraWidth:0.##}" stroke="black" stroke-opacity="0" stroke-linecap="round" stroke-linejoin="round" />
              <title>{title}</title>
            </g>
            {selectedSvg}
            """
        );
    }

    static string SelectedLineSvg(Line line, IReadOnlyList<Pos> polylinePoints)
    {
        if (!line.IsSelected)
            return "";

        var color = DColors.LineSelected;
        var segmentControlColor = DColors.Selected;
        var strokeWidth = line.StrokeWidth;
        var circleRadius = strokeWidth + SelectedCircleExtraRadius;
        var points = ToPolylinePoints(polylinePoints);
        var start = polylinePoints.First();
        var end = polylinePoints.Last();
        var handlesSvg = string.Join(
            "\n",
            line.SegmentPoints.Select(
                (point, index) =>
                {
                    var renderedPoint = polylinePoints[index + 1];
                    var elementId = PointerId.FromLinePoint(line.Id, index).ElementId;
                    // Visible handle plus a larger invisible circle as touch/click hit target.
                    return Invariant(
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

        return Invariant(
            $"""
            <polyline points="{points}" fill="none" stroke="{color}" stroke-width="{strokeWidth
                + SelectedStrokeExtraWidth:0.##}" stroke-linecap="round" stroke-linejoin="round" stroke-dasharray="3,50"/>
            <circle cx="{start.X:0.##}" cy="{start.Y:0.##}" r="{circleRadius:0.##}" fill="{color}" />
            <circle cx="{end.X:0.##}" cy="{end.Y:0.##}" r="{circleRadius:0.##}" fill="{color}" />
            {handlesSvg}
            """
        );
    }

    static string ToPolylinePoints(IReadOnlyList<Pos> points) =>
        string.Join(" ", points.Select(p => Invariant($"{p.X:0.##},{p.Y:0.##}")));
}
