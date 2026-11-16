using Dependinator.UI.Diagrams.Lines;
using Dependinator.UI.Modeling.Models;
using Dependinator.UI.Shared.Types;

namespace Dependinator.UI.Diagrams.Svg;

// Places the labels a container draws (on its sibling and edge lines) that the user has not
// dragged: each goes to the first place along its line, from the middle outwards, where its box is
// clear of the container's nodes and of the labels placed before it, or else where it overlaps
// them the least (Line.AutoLabelPosition). Dragged labels stay where they are and count as taken.
//
// It works in the container's children-local space with the label's size at text zoom 1, and
// looks at every child and every labelled line of the container, not only what the tile draws,
// so a label has the same place in every tile and at every zoom. Shorter lines go first: they
// have the fewest places to choose from.
static class LabelPlacement
{
    // From the middle outwards, alternating sides; never at the ends, where the arrow head and
    // the start circle are.
    static readonly double[] Candidates = [0.5, 0.42, 0.58, 0.34, 0.66, 0.26, 0.74, 0.18, 0.82, 0.12, 0.88];

    public static void Place(Node container)
    {
        List<(Line Line, IReadOnlyList<Pos> Points, Rect Box)>? labels = null;
        foreach (var line in ContainerLines(container))
        {
            if (!line.HasLabel)
                continue;
            var points = LinePathGeometry.GetLocalPolylinePoints(line);
            if (points.Count < 2)
                continue;
            (labels ??= []).Add((line, points, LineSvg.GetLocalLabelBoxAroundPoint(line, points)));
        }
        if (labels is null)
            return;

        var nodes = container
            .Children.Where(child => !child.IsZone && child.Visibility != NodeVisibility.Hidden)
            .Select(NodeArea)
            .ToList();

        // Dragged labels first: they are fixed, the others make room for them.
        var taken = new List<Rect>();
        foreach (var (line, points, box) in labels.Where(label => label.Line.IsLabelMoved))
            taken.Add(BoxAt(points, line.LabelPosition, box));

        var toPlace = labels
            .Where(label => !label.Line.IsLabelMoved)
            .OrderBy(label => Length(label.Points))
            .ThenBy(label => label.Line.Id.Value, StringComparer.Ordinal);
        foreach (var (line, points, box) in toPlace)
        {
            var bestPosition = Line.DefaultLabelPosition;
            var bestBox = BoxAt(points, bestPosition, box);
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
            taken.Add(bestBox);
        }
    }

    // The lines the container draws (as NodeLinesSvg.DrawnNodeLines, without what depends on the
    // tile or the view): from its edge to its children, between them, and from them to its edge.
    static IEnumerable<Line> ContainerLines(Node container)
    {
        foreach (var line in container.SourceLines)
        {
            if (line.Target.Parent == container && line.IsActiveRep)
                yield return line;
        }
        foreach (var child in container.Children)
        {
            foreach (var line in child.SourceLines)
            {
                if (line.IsActiveRep && line.Target.Parent != line.Source)
                    yield return line;
            }
        }
    }

    // The node's box and, below it, the name and description that an icon node draws there
    // (estimated as SvgText draws them at text zoom 1, so like the labels in the container's
    // units). An open container has no texts below it; keeping the room anyway keeps a label's
    // place the same at every zoom.
    static Rect NodeArea(Node node)
    {
        var box = node.Boundary;
        var nameWidth = SvgText.EstimateTextWidth(node.DisplayName, SvgText.FontSize);
        var height = SvgText.FontSize + SvgText.DescriptionLineGap;
        if (!string.IsNullOrWhiteSpace(node.Description))
        {
            var rowHeight = SvgText.DescriptionFontSize * SvgText.DescriptionLineHeightFactor;
            var textWidth = node.Description.Length * SvgText.DescriptionFontSize * SvgText.DescriptionCharWidthFactor;
            var rows = Math.Min(SvgText.IconDescriptionMaxLines, Math.Ceiling(textWidth / Math.Max(box.Width, 1)));
            height += rows * rowHeight;
        }

        var width = Math.Max(box.Width, nameWidth);
        var x = box.X + box.Width / 2 - width / 2;
        return new Rect(Math.Min(box.X, x), box.Y, Math.Max(box.Width, width), box.Height + height);
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

    static double Length(IReadOnlyList<Pos> points)
    {
        var total = 0.0;
        for (var i = 1; i < points.Count; i++)
        {
            var dx = points[i].X - points[i - 1].X;
            var dy = points[i].Y - points[i - 1].Y;
            total += Math.Sqrt(dx * dx + dy * dy);
        }
        return total;
    }
}
