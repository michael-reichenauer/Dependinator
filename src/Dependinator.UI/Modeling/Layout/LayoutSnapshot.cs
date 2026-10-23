using Dependinator.UI.Modeling.Models;
using Dependinator.UI.Shared.Types;

namespace Dependinator.UI.Modeling.Layout;

// A container's children and sibling lines captured as a LayoutGraph, and the way back: applying
// a computed LayoutResult to those children and lines. Capture and Apply run under the model lock;
// the layout in between needs only the graph.
sealed class LayoutSnapshot
{
    readonly IReadOnlyList<Node> nodes;
    readonly IReadOnlyList<Line> lines;

    LayoutSnapshot(LayoutGraph graph, IReadOnlyList<Node> nodes, IReadOnlyList<Line> lines)
    {
        Graph = graph;
        this.nodes = nodes;
        this.lines = lines;
    }

    public LayoutGraph Graph { get; }

    // The arranged children (not pass-through, zones or notes) with their sizes; edges are the
    // aggregated lines between them, and lines to/from the parent itself count as references
    // crossing the parent's edge. Null when there are fewer than two children to arrange.
    public static LayoutSnapshot? Capture(Node parent, LayoutMetrics metrics, Func<Node, Size> sizeOf)
    {
        var children = parent
            .Children.Where(child => !child.IsPassThrough && NodeLayout.IsArranged(child))
            .OrderBy(child => child.Name, StringComparer.Ordinal)
            .ToList();
        if (children.Count < 2)
            return null;

        var indexes = children
            .Select((child, index) => (child, index))
            .ToDictionary(pair => pair.child, pair => pair.index);
        var externalIn = new double[children.Count];
        var externalOut = new double[children.Count];
        var edges = new List<LayoutEdge>();
        var lines = new List<Line>();

        for (var index = 0; index < children.Count; index++)
        {
            foreach (var line in children[index].SourceLines)
            {
                if (!IsLayoutLine(line))
                    continue;
                var weight = Math.Max(1, line.Links.Count);
                if (line.Target == parent)
                {
                    externalOut[index] += weight;
                    continue;
                }
                if (!indexes.TryGetValue(line.Target, out var target) || target == index)
                    continue;

                edges.Add(new LayoutEdge(index, target, weight, !line.IsVisible, line.IsSegmentsUserSet));
                lines.Add(line);
            }

            foreach (var line in children[index].TargetLines)
            {
                if (IsLayoutLine(line) && line.Source == parent)
                    externalIn[index] += Math.Max(1, line.Links.Count);
            }
        }

        var vertices = children
            .Select(
                (child, index) =>
                {
                    var size = sizeOf(child);
                    return new LayoutVertex(child.Name, size.Width, size.Height, externalIn[index], externalOut[index]);
                }
            )
            .ToList();
        var parentAspect =
            parent.Boundary.Height <= 0 ? 1.0 : parent.Boundary.Width / Math.Max(1.0, parent.Boundary.Height);

        return new LayoutSnapshot(new LayoutGraph(vertices, edges, metrics, parentAspect), children, lines);
    }

    // The layout the children have now, as a result (for comparing a computed one with it).
    public LayoutResult CurrentResult() =>
        new(
            [.. nodes.Select(node => node.Boundary)],
            [.. lines.Select(line => (IReadOnlyList<Pos>?)[.. line.SegmentPoints])]
        );

    // Changes whenever anything a layout of the container's children depends on or sets
    // changes: the container's size and transform, its children and their boundaries, and their
    // lines (endpoints, weight, visibility and waypoints). Only compared within one process.
    public static int Fingerprint(Node parent)
    {
        var hash = new HashCode();
        hash.Add(parent.Boundary);
        hash.Add(parent.ContainerZoom);
        hash.Add(parent.ContainerOffset);
        hash.Add(parent.IsChildrenLayoutCustomized);
        hash.Add(parent.Children.Count);
        foreach (var child in parent.Children)
        {
            hash.Add(child.Name);
            hash.Add(child.Boundary);
            foreach (var line in child.SourceLines)
            {
                hash.Add(line.Target.Name);
                hash.Add(line.Links.Count);
                hash.Add(!line.IsVisible);
                hash.Add(line.IsSegmentsUserSet);
                foreach (var point in line.SegmentPoints)
                    hash.Add(point);
            }
        }
        return hash.ToHashCode();
    }

    public void Apply(LayoutResult result)
    {
        for (var index = 0; index < nodes.Count; index++)
            nodes[index].Boundary = result.Boundaries[index];

        for (var index = 0; index < lines.Count; index++)
        {
            if (result.Waypoints[index] is { } waypoints)
                lines[index].SetSegmentPoints(waypoints);
        }
    }

    // The aggregated lines the layered layout arranges by (see NodeSizing.Centrality too).
    static bool IsLayoutLine(Line line) => !line.IsDirect && !line.IsCousin && !line.IsEmpty;
}
