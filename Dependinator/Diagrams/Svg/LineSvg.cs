using Dependinator.Diagrams;
using Dependinator.Models;

namespace Dependinator.Diagrams.Svg;

class LineSvg
{
    public static string GetLineSvg(Line line, Pos nodeCanvasPos, double parentZoom, double childrenZoom)
    {
        if (!ShouldRender(line, parentZoom, childrenZoom))
            return "";

        if (!LinePathGeometry.TryGetLocalEndpoints(line, out var localEndpoints))
            return "";

        var endpoints = LinePathGeometry.ToRendered(localEndpoints, nodeCanvasPos, childrenZoom);
        var polylinePoints = LinePathGeometry.GetRenderedPolylinePoints(line, nodeCanvasPos, childrenZoom);
        var elementId = PointerId.FromLine(line.Id).ElementId;
        UpdateLineOrientation(line, endpoints);

        return BuildLineSvg(line, endpoints, polylinePoints, elementId);
    }

    public static string GetDirectLineSvg(Line line, Node ancestor, Pos nodeCanvasPos, double childrenZoom)
    {
        if (line.RenderAncestor != ancestor)
            return "";

        if (!LinePathGeometry.TryGetLocalEndpoints(line, out var localEndpoints))
            return "";

        var endpoints = LinePathGeometry.ToRendered(localEndpoints, nodeCanvasPos, childrenZoom);
        var polylinePoints = LinePathGeometry.GetRenderedPolylinePoints(line, nodeCanvasPos, childrenZoom);
        var elementId = PointerId.FromLine(line.Id).ElementId;
        UpdateLineOrientation(line, endpoints);

        return BuildLineSvg(line, endpoints, polylinePoints, elementId);
    }

    static bool ShouldRender(Line line, double parentZoom, double childrenZoom)
    {
        if (NodeSvg.IsToLargeToBeSeen(childrenZoom))
            return false;

        var connectsParentAndChild = line.Target.Parent == line.Source || line.Source.Parent == line.Target;
        if (connectsParentAndChild && NodeSvg.IsToLargeToBeSeen(parentZoom))
            return false;

        return true;
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
        var circleRadius = strokeWidth + 1.5;
        var dashArray = line.IsDirect ? " stroke-dasharray=\"6,6\"" : "";
        var points = ToPolylinePoints(polylinePoints);
        var selectedSvg = SelectedLineSvg(line, polylinePoints);

        return $"""
            <polyline points="{points}" fill="none" stroke-width="{strokeWidth}" stroke="{color}" stroke-linecap="round" stroke-linejoin="round" marker-end="url(#{markerId})"{dashArray} />
            <circle cx="{endpoints.X1}" cy="{endpoints.Y1}" r="{circleRadius}" fill="{color}" />
            <g class="hoverable" id="{elementId}">
              <polyline id="{elementId}" points="{points}" fill="none" stroke-width="{strokeWidth
                + 10}" stroke="black" stroke-opacity="0" stroke-linecap="round" stroke-linejoin="round" />
              <title>{line.Source.HtmlLongName}→{line.Target.HtmlLongName} ({line.Links.Count})</title>
            </g>
            {selectedSvg}
            """;
    }

    static string SelectedLineSvg(Line line, IReadOnlyList<Pos> polylinePoints)
    {
        if (!line.IsSelected)
            return "";

        var color = DColors.LineSelected;
        var segmentControlColor = DColors.Selected;
        var strokeWidth = line.StrokeWidth;
        var circleRadius = strokeWidth + 3;
        var points = ToPolylinePoints(polylinePoints);
        var start = polylinePoints.First();
        var end = polylinePoints.Last();
        var handlesSvg = string.Join(
            "\n",
            line.SegmentPoints.Select(
                (point, index) =>
                {
                    var renderedPoint = polylinePoints[index + 1];
                    var handleId = PointerId.FromLinePoint(line.Id, index).ElementId;
                    return $"""
                    <g class="selectpoint">
                      <circle cx="{renderedPoint.X}" cy="{renderedPoint.Y}" r="{circleRadius
                        + 1}" fill="{segmentControlColor}" />
                      <circle id="{handleId}" cx="{renderedPoint.X}" cy="{renderedPoint.Y}" r="{circleRadius
                        + 8}" fill="black" fill-opacity="0" />
                    </g>
                    """;
                }
            )
        );

        return $"""
            <polyline points="{points}" fill="none" stroke="{color}" stroke-width="{strokeWidth
                + 5}" stroke-linecap="round" stroke-linejoin="round" stroke-dasharray="3,50"/>
            <circle cx="{start.X}" cy="{start.Y}" r="{circleRadius}" fill="{color}" />
            <circle cx="{end.X}" cy="{end.Y}" r="{circleRadius}" fill="{color}" />
            {handlesSvg}
            """;
    }

    static void UpdateLineOrientation(Line line, LinePathGeometry.LineEndpoints endpoints)
    {
        line.IsUpHill =
            endpoints.X1 <= endpoints.X2 && endpoints.Y1 >= endpoints.Y2
            || endpoints.X1 >= endpoints.X2 && endpoints.Y1 <= endpoints.Y2;
    }

    static string ToPolylinePoints(IReadOnlyList<Pos> points) => string.Join(" ", points.Select(p => $"{p.X},{p.Y}"));
}
