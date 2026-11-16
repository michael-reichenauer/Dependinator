using Dependinator.UI.Diagrams.Lines;
using Dependinator.UI.Modeling.Models;
using Dependinator.UI.Shared.Types;

namespace Dependinator.UI.Diagrams.Svg;

// Places the labels the user has not dragged: each goes to the first place along its line, from
// the middle outwards, where its box is clear of the nodes of the line's container and of the
// labels placed before it there, or else where it overlaps them the least
// (Line.AutoLabelPosition). Dragged labels stay where they are and count as taken.
//
// It runs over the drawn lines (RepLineService.Sync decides which those are, for the zoom), each
// time they are synced: so the places follow the tile that is shown, which a label drag starts
// from, and not the last tile drawn, which may be one drawn ahead. It works in each container's
// children-local space with the label's size at text zoom 1, and looks at every child and every
// labelled line of the container, not only what a tile draws, so a label has the same place in
// every tile. Shorter lines go first: they have the fewest places to choose from.
static class LabelPlacement
{
    // From the middle outwards, alternating sides; never at the ends, where the arrow head and
    // the start circle are.
    static readonly double[] Candidates = [0.5, 0.42, 0.58, 0.34, 0.66, 0.26, 0.74, 0.18, 0.82, 0.12, 0.88];

    public static void Place(IEnumerable<Line> lines, bool revealHidden, LineFilter filter, RowFilter rows)
    {
        Dictionary<Node, List<(Line Line, IReadOnlyList<Pos> Points, Rect Box)>>? byContainer = null;
        foreach (var line in lines)
        {
            if (!line.HasLabel || !IsDrawn(line, revealHidden, filter, rows))
                continue;
            if (!LinePathGeometry.TryGetOwnerNode(line, out var container) || IsDegenerate(line, container))
                continue;
            var points = LinePathGeometry.GetLocalPolylinePoints(line);
            if (points.Count < 2)
                continue;

            byContainer ??= [];
            if (!byContainer.TryGetValue(container, out var labels))
                byContainer[container] = labels = [];
            labels.Add((line, points, LineSvg.GetLocalLabelBoxAroundPoint(line, points)));
        }
        if (byContainer is null)
            return;

        foreach (var (container, labels) in byContainer)
            Place(container, labels, revealHidden);
    }

    static void Place(Node container, List<(Line Line, IReadOnlyList<Pos> Points, Rect Box)> labels, bool revealHidden)
    {
        var nodes = new List<Rect>();
        foreach (var child in container.Children)
        {
            if (child.IsZone || child.IsFoldedAway || child.Visibility == NodeVisibility.Hidden && !revealHidden)
                continue;
            nodes.Add(child.Boundary);
            if (TextBelow(child) is { } text)
                nodes.Add(text);
        }

        // Dragged labels first: they are fixed, the others make room for them.
        var taken = new List<Rect>();
        foreach (var (line, points, box) in labels.Where(label => label.Line.IsLabelMoved))
            taken.Add(BoxAt(points, line.LabelPosition, box));

        var toPlace = labels
            .Where(label => !label.Line.IsLabelMoved)
            .OrderBy(label => LinePathGeometry.Length(label.Points))
            .ThenBy(label => label.Line.Id.Value, StringComparer.Ordinal);
        foreach (var (line, points, box) in toPlace)
        {
            var bestPosition = Line.DefaultLabelPosition;
            Rect? bestBox = null;
            var bestOverlap = double.MaxValue;
            foreach (var position in Candidates)
            {
                var candidate = BoxAt(points, position, box);
                var overlap = Overlap(candidate, nodes) + Overlap(candidate, taken);
                if (overlap >= bestOverlap)
                    continue;
                (bestPosition, bestBox, bestOverlap) = (position, candidate, overlap);
                if (overlap == 0)
                    break;
            }

            line.AutoLabelPosition = bestPosition;
            taken.Add(bestBox!);
        }
    }

    // As NodeLinesSvg draws a container's lines, without what depends on the tile.
    static bool IsDrawn(Line line, bool revealHidden, LineFilter filter, RowFilter rows)
    {
        // A user's direct line is drawn whatever the line representatives are.
        return (line.IsDirect || line.IsActiveRep)
            && (line.Visibility != NodeVisibility.Hidden || revealHidden)
            && !LineEmphasis.IsLineFilteredOut(line, filter, rows);
    }

    // A pass-through node covers its parent, so the segment between them is not drawn.
    static bool IsDegenerate(Line line, Node container) =>
        !line.IsDirect
        && line.RenderAncestor is null
        && (
            line.Source == container && line.Target.IsPassThrough
            || line.Target == container && line.Source.IsPassThrough
        );

    // The name and description that an icon node draws below its box, estimated as NodeSvg
    // draws them at text zoom 1 (so like the labels, in the container's units). A member row
    // draws its texts inside its box. An open container has none below it either; keeping the
    // room anyway keeps a label's place when the zoom opens the node.
    static Rect? TextBelow(Node node)
    {
        if (node.Type.IsMember)
            return null;

        var box = node.Boundary;
        var width = SvgText.EstimateTextWidth(node.DisplayName, SvgText.FontSize);
        var height = SvgText.FontSize + SvgText.DescriptionLineGap;
        var rows = SvgText.GetDescriptionLines(
            node.Description,
            SvgText.DescriptionMinWidth,
            SvgText.IconDescriptionMaxLines
        );
        if (rows.Count > 0)
        {
            var charWidth = SvgText.DescriptionFontSize * SvgText.DescriptionCharWidthFactor;
            width = Math.Max(width, rows.Max(row => row.Length) * charWidth);
            height += rows.Count * SvgText.DescriptionFontSize * SvgText.DescriptionLineHeightFactor;
        }

        return new Rect(box.X + box.Width / 2 - width / 2, box.Y + box.Height, width, height);
    }

    static Rect BoxAt(IReadOnlyList<Pos> points, double position, Rect boxAroundPoint)
    {
        var at = LinePathGeometry.PointAt(points, position);
        return new Rect(at.X + boxAroundPoint.X, at.Y + boxAroundPoint.Y, boxAroundPoint.Width, boxAroundPoint.Height);
    }

    // The area the box shares with the others.
    static double Overlap(Rect box, List<Rect> others)
    {
        var total = 0.0;
        foreach (var other in others)
        {
            var width = Math.Min(box.X + box.Width, other.X + other.Width) - Math.Max(box.X, other.X);
            var height = Math.Min(box.Y + box.Height, other.Y + other.Height) - Math.Max(box.Y, other.Y);
            if (width > 0 && height > 0)
                total += width * height;
        }
        return total;
    }
}
