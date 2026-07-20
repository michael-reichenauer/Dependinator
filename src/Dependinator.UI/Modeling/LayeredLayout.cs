using Dependinator.UI.Modeling.Models;
using Dependinator.UI.Shared.Types;

namespace Dependinator.UI.Modeling;

// Arranges a parent's children as a layered, left-to-right dependency flow (Sugiyama style):
// externally referenced entry nodes at the left (external references always enter through the
// parent's left edge), dependencies flowing rightward toward utility/sink nodes, and nodes
// without any relations packed at the bottom right. The layout is deterministic; all ties are
// broken by node name.
static class LayeredLayout
{
    const int MaxOrderingSweeps = 8;

    public static bool TryArrange(Node parent, LayoutMetrics metrics)
    {
        var graph = BuildGraph(parent);
        if (graph is null)
            return false;

        RemoveCycles(graph);
        AssignLayers(graph);
        CompactLayers(graph, parent, metrics);
        OrderLayers(graph);
        AssignCoordinates(graph, metrics);
        PlaceIsolated(graph, metrics);
        RouteLines(graph, metrics);
        return true;
    }

    // Builds the sibling dependency graph: edges are the aggregated lines between children,
    // and lines to/from the parent itself represent references crossing the parent boundary
    // (in by construction from the left, out to the right). Returns null when there are no
    // relations at all, so the caller can fall back to the plain grid layout.
    static Graph? BuildGraph(Node parent)
    {
        var children = parent
            .Children.Where(child => !child.IsPassThrough)
            .OrderBy(child => child.Name, StringComparer.Ordinal)
            .ToList();
        if (children.Count < 2)
            return null;

        var graphNodes = children.Select(child => new GraphNode(child)).ToList();
        var nodesByChild = graphNodes.ToDictionary(graphNode => graphNode.Node);
        var edges = new List<GraphEdge>();

        foreach (var graphNode in graphNodes)
        {
            foreach (var line in graphNode.Node.SourceLines)
            {
                if (line.IsDirect || line.IsCousin || line.IsEmpty)
                    continue;
                var weight = Math.Max(1, line.Links.Count);
                if (line.Target == parent)
                {
                    graphNode.ExternalOut += weight;
                    continue;
                }
                if (!nodesByChild.TryGetValue(line.Target, out var target) || target == graphNode)
                    continue;

                var edge = new GraphEdge(graphNode, target, weight, line);
                edges.Add(edge);
                graphNode.OutEdges.Add(edge);
                target.InEdges.Add(edge);
            }

            foreach (var line in graphNode.Node.TargetLines)
            {
                if (line.IsDirect || line.IsCousin || line.IsEmpty)
                    continue;
                if (line.Source == parent)
                    graphNode.ExternalIn += Math.Max(1, line.Links.Count);
            }
        }

        foreach (var graphNode in graphNodes)
        {
            graphNode.Importance =
                graphNode.ExternalIn
                + graphNode.ExternalOut
                + graphNode.InEdges.Sum(edge => edge.Weight)
                + graphNode.OutEdges.Sum(edge => edge.Weight);
        }

        if (graphNodes.All(graphNode => graphNode.Importance == 0))
            return null;

        var isolated = graphNodes.Where(graphNode => graphNode.Importance == 0).ToList();
        var layered = graphNodes.Where(graphNode => graphNode.Importance > 0).ToList();
        return new Graph(layered, isolated, edges);
    }

    // Greedy Eades-Lin-Smyth feedback arc set: builds a node sequence by repeatedly peeling
    // sinks (to the tail) and sources (to the head), otherwise picking the node with the
    // largest weighted out-in difference. Edges pointing backward in the sequence are marked
    // reversed, making the effective graph acyclic; the sequence is a topological order of it.
    static void RemoveCycles(Graph graph)
    {
        var remaining = new HashSet<GraphNode>(graph.Nodes);
        var head = new List<GraphNode>();
        var tail = new List<GraphNode>();

        double OutWeight(GraphNode node) =>
            node.OutEdges.Where(edge => remaining.Contains(edge.Target)).Sum(edge => edge.Weight);
        double InWeight(GraphNode node) =>
            node.InEdges.Where(edge => remaining.Contains(edge.Source)).Sum(edge => edge.Weight);

        while (remaining.Count > 0)
        {
            var changed = true;
            while (changed && remaining.Count > 0)
            {
                changed = false;

                var sinks = remaining
                    .Where(node => OutWeight(node) == 0)
                    .OrderBy(node => node.Node.Name, StringComparer.Ordinal)
                    .ToList();
                foreach (var sink in sinks)
                {
                    tail.Add(sink);
                    remaining.Remove(sink);
                    changed = true;
                }

                var sources = remaining
                    .Where(node => InWeight(node) == 0)
                    .OrderBy(node => node.Node.Name, StringComparer.Ordinal)
                    .ToList();
                foreach (var source in sources)
                {
                    head.Add(source);
                    remaining.Remove(source);
                    changed = true;
                }
            }

            if (remaining.Count == 0)
                break;

            var pick = remaining
                .OrderByDescending(node => OutWeight(node) - InWeight(node))
                .ThenByDescending(node => node.Importance)
                .ThenBy(node => node.Node.Name, StringComparer.Ordinal)
                .First();
            head.Add(pick);
            remaining.Remove(pick);
        }

        tail.Reverse();
        var order = head.Concat(tail).ToList();
        for (var index = 0; index < order.Count; index++)
        {
            order[index].SequenceIndex = index;
        }

        foreach (var edge in graph.Edges)
        {
            edge.IsReversed = edge.Source.SequenceIndex > edge.Target.SequenceIndex;
        }

        graph.TopologicalOrder = order;
    }

    // Longest-path layering over the effective (acyclic) edges. Entry nodes - children whose
    // external inbound references outweigh their internal ones - are pinned to the leftmost
    // layer, since their references physically arrive at the parent's left edge. Children that
    // only reference the outside world (no sibling relations) drift to the rightmost layer.
    static void AssignLayers(Graph graph)
    {
        foreach (var node in graph.Nodes)
        {
            var internalInWeight = node
                .InEdges.Where(edge => !edge.IsReversed)
                .Concat(node.OutEdges.Where(edge => edge.IsReversed))
                .Sum(edge => edge.Weight);
            node.IsEntry = node.ExternalIn > 0 && node.ExternalIn >= internalInWeight;
        }

        foreach (var node in graph.TopologicalOrder)
        {
            if (node.IsEntry)
            {
                node.Layer = 0;
                continue;
            }

            var layer = 0;
            foreach (var edge in node.InEdges.Where(edge => !edge.IsReversed))
                layer = Math.Max(layer, edge.Source.Layer + 1);
            foreach (var edge in node.OutEdges.Where(edge => edge.IsReversed))
                layer = Math.Max(layer, edge.Target.Layer + 1);
            node.Layer = layer;
        }

        graph.LayerCount = graph.Nodes.Max(node => node.Layer) + 1;

        var externalOutOnly = graph.Nodes.Where(node =>
            node.InEdges.Count == 0 && node.OutEdges.Count == 0 && node.ExternalIn == 0
        );
        foreach (var node in externalOutOnly)
        {
            node.Layer = graph.LayerCount - 1;
        }
    }

    // Folds the layering to roughly match the parent's aspect ratio, so the container transform
    // does not shrink a long dependency chain into a thin strip. Layers are merged by
    // proportional scaling; over-tall layers are later wrapped into adjacent sub-columns.
    static void CompactLayers(Graph graph, Node parent, LayoutMetrics metrics)
    {
        var nodeCount = graph.Nodes.Count;
        var cellWidth = metrics.Width + metrics.HorizontalGap;
        var cellHeight = metrics.Height + metrics.VerticalGap;
        var parentAspect =
            parent.Boundary.Height <= 0 ? 1.0 : parent.Boundary.Width / Math.Max(1.0, parent.Boundary.Height);

        // Ceiling biases toward more columns, since left-to-right flow is the primary axis
        var maxLayers = Math.Clamp(
            (int)Math.Ceiling(Math.Sqrt(nodeCount * parentAspect * cellHeight / cellWidth)),
            1,
            nodeCount
        );

        if (graph.LayerCount > maxLayers)
        {
            var scale = (maxLayers - 1.0) / (graph.LayerCount - 1.0);
            foreach (var node in graph.Nodes)
            {
                node.Layer = (int)Math.Round(node.Layer * scale);
            }
            graph.LayerCount = maxLayers;
        }

        graph.MaxRowsPerColumn = (int)Math.Ceiling(nodeCount / (double)maxLayers) + 1;
    }

    // Weighted barycenter sweeps to reduce crossings and keep related nodes adjacent. External
    // inbound references act as an anchor at the top of the left edge, giving entry nodes a
    // pull toward the top. Positions are normalized per layer so barycenters from layers of
    // different sizes are comparable.
    static void OrderLayers(Graph graph)
    {
        var layers = Enumerable
            .Range(0, graph.LayerCount)
            .Select(layerIndex =>
                graph
                    .Nodes.Where(node => node.Layer == layerIndex)
                    .OrderByDescending(node => node.ExternalIn)
                    .ThenByDescending(node => node.Importance)
                    .ThenBy(node => node.Node.Name, StringComparer.Ordinal)
                    .ToList()
            )
            .ToList();

        foreach (var layer in layers)
            SetPositions(layer);

        for (var sweep = 0; sweep < MaxOrderingSweeps; sweep++)
        {
            var changed = false;

            for (var layerIndex = 0; layerIndex < layers.Count; layerIndex++)
                changed |= ReorderLayer(layers[layerIndex], toLeft: true);

            for (var layerIndex = layers.Count - 1; layerIndex >= 0; layerIndex--)
                changed |= ReorderLayer(layers[layerIndex], toLeft: false);

            if (!changed)
                break;
        }
    }

    static void SetPositions(List<GraphNode> layer)
    {
        for (var index = 0; index < layer.Count; index++)
        {
            layer[index].Position = layer.Count == 1 ? 0.5 : index / (double)(layer.Count - 1);
        }
    }

    static bool ReorderLayer(List<GraphNode> layer, bool toLeft)
    {
        if (layer.Count < 2)
            return false;

        var ordered = layer
            .OrderBy(node => Barycenter(node, toLeft))
            .ThenBy(node => node.Position)
            .ThenBy(node => node.Node.Name, StringComparer.Ordinal)
            .ToList();

        var changed = !ordered.SequenceEqual(layer);
        if (changed)
        {
            layer.Clear();
            layer.AddRange(ordered);
            SetPositions(layer);
        }
        return changed;
    }

    // Average normalized position of the node's neighbors on the given side, weighted by link
    // count. External inbound references pull toward normalized position 0 (top left).
    static double Barycenter(GraphNode node, bool toLeft)
    {
        var weightSum = 0.0;
        var positionSum = 0.0;

        foreach (var (neighbor, weight) in Neighbors(node))
        {
            var isOnSide = toLeft ? neighbor.Layer < node.Layer : neighbor.Layer > node.Layer;
            if (!isOnSide)
                continue;
            weightSum += weight;
            positionSum += neighbor.Position * weight;
        }

        if (toLeft && node.ExternalIn > 0)
        {
            weightSum += node.ExternalIn;
        }

        return weightSum == 0 ? node.Position : positionSum / weightSum;
    }

    static IEnumerable<(GraphNode Neighbor, double Weight)> Neighbors(GraphNode node)
    {
        foreach (var edge in node.OutEdges)
            yield return (edge.Target, edge.Weight);
        foreach (var edge in node.InEdges)
            yield return (edge.Source, edge.Weight);
    }

    // Places layers as columns left to right, wrapping over-tall layers into adjacent
    // sub-columns. A priority pass then pulls each node toward the average vertical position
    // of its already placed left-side neighbors, preserving order and minimum gaps.
    static void AssignCoordinates(Graph graph, LayoutMetrics metrics)
    {
        var cellWidth = metrics.Width + metrics.HorizontalGap;
        var cellHeight = metrics.Height + metrics.VerticalGap;
        var columns = new List<List<GraphNode>>();

        for (var layerIndex = 0; layerIndex < graph.LayerCount; layerIndex++)
        {
            var layerNodes = graph
                .Nodes.Where(node => node.Layer == layerIndex)
                .OrderBy(node => node.Position)
                .ThenBy(node => node.Node.Name, StringComparer.Ordinal)
                .ToList();

            for (var start = 0; start < layerNodes.Count; start += graph.MaxRowsPerColumn)
            {
                columns.Add(layerNodes.Skip(start).Take(graph.MaxRowsPerColumn).ToList());
            }
        }

        for (var columnIndex = 0; columnIndex < columns.Count; columnIndex++)
        {
            var column = columns[columnIndex];
            var x = metrics.HorizontalGap + columnIndex * cellWidth;
            var previousBottom = 0.0;

            for (var row = 0; row < column.Count; row++)
            {
                var node = column[row];
                var slotY = metrics.VerticalGap + row * cellHeight;
                var desiredY = DesiredY(node, columnIndex, columns, slotY);
                var y = Math.Max(desiredY, row == 0 ? metrics.VerticalGap : previousBottom + metrics.VerticalGap);

                node.Node.Boundary = NodeLayout.SnapPositionToGrid(new Rect(x, y, metrics.Width, metrics.Height));
                node.ColumnIndex = columnIndex;
                previousBottom = node.Node.Boundary.Y + metrics.Height;
            }
        }

        graph.ColumnCount = columns.Count;
    }

    static double DesiredY(GraphNode node, int columnIndex, List<List<GraphNode>> columns, double slotY)
    {
        var weightSum = 0.0;
        var ySum = 0.0;

        foreach (var (neighbor, weight) in Neighbors(node))
        {
            if (neighbor.ColumnIndex is not int neighborColumn || neighborColumn >= columnIndex)
                continue;
            weightSum += weight;
            ySum += neighbor.Node.Boundary.Y * weight;
        }

        return weightSum == 0 ? slotY : ySum / weightSum;
    }

    // Nodes without any relations are utility/leftover nodes; pack them as a block at the
    // bottom right of the layered content, ordered by importance and name.
    static void PlaceIsolated(Graph graph, LayoutMetrics metrics)
    {
        if (graph.Isolated.Count == 0)
            return;

        var cellWidth = metrics.Width + metrics.HorizontalGap;
        var cellHeight = metrics.Height + metrics.VerticalGap;

        var isolated = graph.Isolated.OrderBy(node => node.Node.Name, StringComparer.Ordinal).ToList();
        var blockColumns = (int)Math.Ceiling(Math.Sqrt(isolated.Count));
        var blockRows = (int)Math.Ceiling(isolated.Count / (double)blockColumns);

        var startX = metrics.HorizontalGap + graph.ColumnCount * cellWidth;
        var contentBottom =
            graph.Nodes.Count == 0
                ? metrics.VerticalGap
                : graph.Nodes.Max(node => node.Node.Boundary.Y + node.Node.Boundary.Height);
        var startY = Math.Max(metrics.VerticalGap, contentBottom - (blockRows * cellHeight - metrics.VerticalGap));

        for (var index = 0; index < isolated.Count; index++)
        {
            var column = index % blockColumns;
            var row = index / blockColumns;
            var x = startX + column * cellWidth;
            var y = startY + row * cellHeight;
            isolated[index].Node.Boundary = NodeLayout.SnapPositionToGrid(
                new Rect(x, y, metrics.Width, metrics.Height)
            );
        }
    }

    // Routes lines that span more than one column via waypoints in the row gaps of the
    // intermediate columns, so they do not cut straight through nodes. Straight lines between
    // adjacent columns are left alone. Hidden lines are skipped (stale auto waypoints cleared),
    // and lines whose waypoints the user placed are never touched.
    static void RouteLines(Graph graph, LayoutMetrics metrics)
    {
        var columns = graph
            .Nodes.GroupBy(node => node.ColumnIndex ?? 0)
            .ToDictionary(
                group => group.Key,
                group => group.Select(node => node.Node.Boundary).OrderBy(rect => rect.Y).ToList()
            );

        foreach (var edge in graph.Edges)
        {
            var line = edge.Line;
            if (line.IsSegmentsUserSet)
                continue;

            var sourceColumn = edge.Source.ColumnIndex ?? 0;
            var targetColumn = edge.Target.ColumnIndex ?? 0;
            if (line.IsHidden || Math.Abs(sourceColumn - targetColumn) <= 1)
            {
                line.SetSegmentPoints([]);
                continue;
            }

            line.SetSegmentPoints(RouteSkipEdge(edge, sourceColumn, targetColumn, columns, metrics));
        }
    }

    static List<Pos> RouteSkipEdge(
        GraphEdge edge,
        int sourceColumn,
        int targetColumn,
        Dictionary<int, List<Rect>> columns,
        LayoutMetrics metrics
    )
    {
        var goingRight = targetColumn > sourceColumn;
        var sourceRect = edge.Source.Node.Boundary;
        var targetRect = edge.Target.Node.Boundary;

        // The anchors the renderer uses: source leaves from an edge center, target enters one
        var start = new Pos(goingRight ? sourceRect.X + sourceRect.Width : sourceRect.X, CenterY(sourceRect));
        var end = new Pos(goingRight ? targetRect.X : targetRect.X + targetRect.Width, CenterY(targetRect));

        var cellWidth = metrics.Width + metrics.HorizontalGap;
        var points = new List<Pos>();
        var step = goingRight ? 1 : -1;

        for (var column = sourceColumn + step; column != targetColumn; column += step)
        {
            if (!columns.TryGetValue(column, out var rects) || rects.Count == 0)
                continue;

            var columnCenterX = metrics.HorizontalGap + column * cellWidth + metrics.Width / 2;
            var straightY = InterpolateY(start, end, columnCenterX);
            if (!CrossesNode(rects, straightY))
                continue;

            var gapY = NearestGapY(rects, straightY, metrics);
            points.Add(new Pos(columnCenterX, gapY));
        }

        return points;
    }

    static double CenterY(Rect rect) => rect.Y + rect.Height / 2;

    static double InterpolateY(Pos start, Pos end, double x) =>
        start.Y + (end.Y - start.Y) * ((x - start.X) / (end.X - start.X));

    const double RouteClearance = 8;

    static bool CrossesNode(List<Rect> rects, double y) =>
        rects.Any(rect => y >= rect.Y - RouteClearance && y <= rect.Y + rect.Height + RouteClearance);

    // Candidate detour heights are the centers of the gaps between the column's rows, plus one
    // above the topmost and one below the bottommost node; picks the one closest to the
    // straight line's height.
    static double NearestGapY(List<Rect> rects, double y, LayoutMetrics metrics)
    {
        var halfGap = metrics.VerticalGap / 2;
        var candidates = new List<double> { rects[0].Y - halfGap };
        for (var index = 0; index < rects.Count - 1; index++)
        {
            candidates.Add((rects[index].Y + rects[index].Height + rects[index + 1].Y) / 2);
        }
        candidates.Add(rects[^1].Y + rects[^1].Height + halfGap);

        return candidates.OrderBy(candidate => Math.Abs(candidate - y)).First();
    }

    class Graph(List<GraphNode> nodes, List<GraphNode> isolated, List<GraphEdge> edges)
    {
        public List<GraphNode> Nodes { get; } = nodes;
        public List<GraphNode> Isolated { get; } = isolated;
        public List<GraphEdge> Edges { get; } = edges;
        public List<GraphNode> TopologicalOrder { get; set; } = [];
        public int LayerCount { get; set; }
        public int MaxRowsPerColumn { get; set; } = 1;
        public int ColumnCount { get; set; }
    }

    class GraphNode(Node node)
    {
        public Node Node { get; } = node;
        public List<GraphEdge> OutEdges { get; } = [];
        public List<GraphEdge> InEdges { get; } = [];
        public double ExternalIn { get; set; }
        public double ExternalOut { get; set; }
        public double Importance { get; set; }
        public bool IsEntry { get; set; }
        public int SequenceIndex { get; set; }
        public int Layer { get; set; }
        public double Position { get; set; }
        public int? ColumnIndex { get; set; }
    }

    class GraphEdge(GraphNode source, GraphNode target, double weight, Line line)
    {
        public GraphNode Source { get; } = source;
        public GraphNode Target { get; } = target;
        public double Weight { get; } = weight;
        public Line Line { get; } = line;
        public bool IsReversed { get; set; }
    }
}
