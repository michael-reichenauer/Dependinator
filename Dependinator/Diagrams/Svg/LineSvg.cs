using System;
using Dependinator.Models;

namespace Dependinator.Diagrams.Svg;

class LineSvg
{
    public static string GetLineSvg(Line line, Pos nodeCanvasPos, double parentZoom, double childrenZoom)
    {
        if (!ShouldRender(line, parentZoom, childrenZoom))
            return "";

        var endpoints = CalculateLineEndpoints(line, nodeCanvasPos, parentZoom, childrenZoom);
        var elementId = PointerId.FromLine(line.Id).ElementId;
        UpdateLineOrientation(line, endpoints);

        return BuildLineSvg(line, endpoints, elementId);
    }

    public static string GetDirectLineSvg(Line line, Node ancestor, Pos nodeCanvasPos, double childrenZoom)
    {
        if (line.RenderAncestor != ancestor)
            return "";

        var (sourceAnchor, targetAnchor) = DirectLineCalculator.GetAnchorsRelativeToAncestor(
            ancestor,
            line.Source,
            line.Target
        );

        var endpoints = new LineEndpoints(
            nodeCanvasPos.X + sourceAnchor.X * childrenZoom,
            nodeCanvasPos.Y + sourceAnchor.Y * childrenZoom,
            nodeCanvasPos.X + targetAnchor.X * childrenZoom,
            nodeCanvasPos.Y + targetAnchor.Y * childrenZoom
        );

        var elementId = PointerId.FromLine(line.Id).ElementId;
        UpdateLineOrientation(line, endpoints);

        return BuildLineSvg(line, endpoints, elementId);
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

    static LineEndpoints CalculateLineEndpoints(Line line, Pos nodeCanvasPos, double parentZoom, double childrenZoom)
    {
        var sourceBoundary = line.Source.Boundary;
        var targetBoundary = line.Target.Boundary;

        var sourceAnchor = GetAnchor(line.Source, AnchorSide.Right);
        var targetAnchor = GetAnchor(line.Target, AnchorSide.Left);

        return GetRelation(line) switch
        {
            LineRelation.ParentToChild => ParentToChildEndpoints(
                line,
                nodeCanvasPos,
                parentZoom,
                childrenZoom,
                sourceBoundary,
                targetAnchor
            ),
            LineRelation.ChildToParent => ChildToParentEndpoints(
                line,
                nodeCanvasPos,
                parentZoom,
                childrenZoom,
                sourceAnchor,
                targetBoundary
            ),
            _ => SiblingEndpoints(nodeCanvasPos, childrenZoom, sourceAnchor, targetAnchor),
        };
    }

    static LineEndpoints ParentToChildEndpoints(
        Line line,
        Pos nodeCanvasPos,
        double parentZoom,
        double childrenZoom,
        Rect sourceBoundary,
        (double X, double Y) targetAnchor
    )
    {
        var parent = line.Source;
        var x1 = nodeCanvasPos.X - parent.ContainerOffset.X * parentZoom;
        var y1 = nodeCanvasPos.Y + sourceBoundary.Height / 2 * parentZoom - parent.ContainerOffset.Y * parentZoom;
        var x2 = nodeCanvasPos.X + targetAnchor.X * childrenZoom;
        var y2 = nodeCanvasPos.Y + targetAnchor.Y * childrenZoom;
        return new LineEndpoints(x1, y1, x2, y2);
    }

    static LineEndpoints ChildToParentEndpoints(
        Line line,
        Pos nodeCanvasPos,
        double parentZoom,
        double childrenZoom,
        (double X, double Y) sourceAnchor,
        Rect targetBoundary
    )
    {
        var parent = line.Target;
        var x1 = nodeCanvasPos.X + sourceAnchor.X * childrenZoom;
        var y1 = nodeCanvasPos.Y + sourceAnchor.Y * childrenZoom;
        var x2 = nodeCanvasPos.X + targetBoundary.Width * parentZoom - parent.ContainerOffset.X * parentZoom;
        var y2 = nodeCanvasPos.Y + targetBoundary.Height / 2 * parentZoom - parent.ContainerOffset.Y * parentZoom;
        return new LineEndpoints(x1, y1, x2, y2);
    }

    static LineEndpoints SiblingEndpoints(
        Pos nodeCanvasPos,
        double childrenZoom,
        (double X, double Y) sourceAnchor,
        (double X, double Y) targetAnchor
    )
    {
        var x1 = nodeCanvasPos.X + sourceAnchor.X * childrenZoom;
        var y1 = nodeCanvasPos.Y + sourceAnchor.Y * childrenZoom;
        var x2 = nodeCanvasPos.X + targetAnchor.X * childrenZoom;
        var y2 = nodeCanvasPos.Y + targetAnchor.Y * childrenZoom;
        return new LineEndpoints(x1, y1, x2, y2);
    }

    static string BuildLineSvg(Line line, LineEndpoints endpoints, string elementId)
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
        var selectedSvg = SelectedLineSvg(line, endpoints);

        return $"""
            <line x1="{endpoints.X1}" y1="{endpoints.Y1}" x2="{endpoints.X2}" y2="{endpoints.Y2}" stroke-width="{strokeWidth}" stroke="{color}" marker-end="url(#{markerId})"{dashArray} />
            <circle cx="{endpoints.X1}" cy="{endpoints.Y1}" r="{circleRadius}" fill="{color}" />
            <g class="hoverable" id="{elementId}">
              <line id="{elementId}" x1="{endpoints.X1}" y1="{endpoints.Y1}" x2="{endpoints.X2}" y2="{endpoints.Y2}" stroke-width="{strokeWidth
                + 10}" stroke="black" stroke-opacity="0" />
              <title>{line.Source.HtmlLongName}→{line.Target.HtmlLongName} ({line.Links.Count})</title>
            </g>
            {selectedSvg}
            """;
    }

    static string SelectedLineSvg(Line line, LineEndpoints endpoints)
    {
        if (!line.IsSelected)
            return "";

        var color = DColors.Selected;
        var strokeWidth = line.StrokeWidth;
        var circleRadius = strokeWidth + 3;

        return $"""
            <line x1="{endpoints.X1}" y1="{endpoints.Y1}" x2="{endpoints.X2}" y2="{endpoints.Y2}" stroke="{color}" stroke-width="{strokeWidth
                + 5}" stroke-dasharray="3,50"/>
            <circle cx="{endpoints.X1}" cy="{endpoints.Y1}" r="{circleRadius}" fill="{color}" />
            <circle cx="{endpoints.X2}" cy="{endpoints.Y2}" r="{circleRadius}" fill="{color}" />
            """;
    }

    static void UpdateLineOrientation(Line line, LineEndpoints endpoints)
    {
        line.IsUpHill =
            endpoints.X1 <= endpoints.X2 && endpoints.Y1 >= endpoints.Y2
            || endpoints.X1 >= endpoints.X2 && endpoints.Y1 <= endpoints.Y2;
    }

    static LineRelation GetRelation(Line line)
    {
        if (line.Target.Parent == line.Source)
            return LineRelation.ParentToChild;
        if (line.Source.Parent == line.Target)
            return LineRelation.ChildToParent;
        return LineRelation.Sibling;
    }

    enum LineRelation
    {
        ParentToChild,
        ChildToParent,
        Sibling,
    }

    enum AnchorSide
    {
        Left,
        Right,
    }

    static (double X, double Y) GetAnchor(Node node, AnchorSide side)
    {
        var offsetX = NodeSvg.GetHorizontalAnchorOffset(node, side == AnchorSide.Right);
        var boundary = node.Boundary;
        return (boundary.X + offsetX, boundary.Y + boundary.Height / 2);
    }

    readonly record struct LineEndpoints(double X1, double Y1, double X2, double Y2);
}
