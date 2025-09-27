using Dependinator.Models;

namespace Dependinator.Diagrams.Svg;

class LineSvg
{
    public static string GetLineSvg(Line line, Pos nodeCanvasPos, double parentZoom, double childrenZoom)
    {
        if (line.IsHidden || NodeSvg.IsToLargeToBeSeen(childrenZoom))
            return "";

        if (
            (line.Target.Parent == line.Source || line.Source.Parent == line.Target)
            && NodeSvg.IsToLargeToBeSeen(parentZoom)
        )
        {
            return "";
        }

        var (s, t) = (line.Source.Boundary, line.Target.Boundary);

        var (x1, y1) = (s.X + s.Width, s.Y + s.Height / 2);
        var (x2, y2) = (t.X, t.Y + t.Height / 2);

        if (line.Target.Parent == line.Source)
        { // Parent source to child target (left of parent to right of child)
            var parent = line.Source;
            (x1, y1) = (
                nodeCanvasPos.X - parent.ContainerOffset.X * parentZoom,
                nodeCanvasPos.Y + s.Height / 2 * parentZoom - parent.ContainerOffset.Y * parentZoom
            );
            (x2, y2) = (nodeCanvasPos.X + x2 * childrenZoom, nodeCanvasPos.Y + y2 * childrenZoom);
        }
        else if (line.Source.Parent == line.Target)
        { // Child source to parent target (left of child to right of parent)
            var parent = line.Target;
            (x1, y1) = (nodeCanvasPos.X + x1 * childrenZoom, nodeCanvasPos.Y + y1 * childrenZoom);

            (x2, y2) = (
                nodeCanvasPos.X + t.Width * parentZoom - parent.ContainerOffset.X * parentZoom,
                nodeCanvasPos.Y + t.Height / 2 * parentZoom - parent.ContainerOffset.Y * parentZoom
            );
        }
        else
        { // Sibling source to sibling target (right of source to left of target)
            (x1, y1) = (nodeCanvasPos.X + x1 * childrenZoom, nodeCanvasPos.Y + y1 * childrenZoom);
            (x2, y2) = (nodeCanvasPos.X + x2 * childrenZoom, nodeCanvasPos.Y + y2 * childrenZoom);
        }

        var sw = line.StrokeWidth;
        var elementId = PointerId.FromLine(line.Id).ElementId;
        string selectedSvg = SelectedLineSvg(line, x1, y1, x2, y2);

        line.IsUpHill = x1 <= x2 && y1 >= y2 || x1 >= x2 && y1 <= y2;

        var c = DColors.Line;

        return $"""
            <line x1="{x1}" y1="{y1}" x2="{x2}" y2="{y2}" stroke-width="{sw}" stroke="{c}" marker-end="url(#arrow)" />
            <circle cx="{x1}" cy="{y1}" r="3" fill="{c}" />
            <g class="hoverable" id="{elementId}">
              <line id="{elementId}" x1="{x1}" y1="{y1}" x2="{x2}" y2="{y2}" stroke-width="{sw
                + 10}" stroke="black" stroke-opacity="0" />
              <title>{line.Source.HtmlLongName}→{line.Target.HtmlLongName}</title>
            </g>
            {selectedSvg}
            """;
    }

    private static string SelectedLineSvg(Line line, double x1, double y1, double x2, double y2)
    {
        if (!line.IsSelected)
            return "";

        string c = DColors.Selected;
        var sw = line.StrokeWidth;
        var ps = 7;

        return $"""
            <line x1="{x1}" y1="{y1}" x2="{x2}" y2="{y2}" stroke="{c}" stroke-width="{sw
                + 5}" stroke-dasharray="3,50"/>
            <circle cx="{x1}" cy="{y1}" r="{ps}" fill="{c}" />
            <circle cx="{x2}" cy="{y2}" r="{ps}" fill="{c}" />
            """;
    }
}
