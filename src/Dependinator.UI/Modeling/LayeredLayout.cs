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

    // Sweeps of the vertical placement (see AssignCoordinates): first along the flow, pulled by
    // the left (upstream) neighbors only, then back and forth pulled by both sides.
    static readonly (bool IsLeftToRight, bool UseLeft, bool UseRight)[] PlacementSweeps =
    [
        (true, true, false),
        (false, true, true),
        (true, true, true),
    ];

    // Pull of a node's own current height in a placement sweep when no neighbor pulls it: weak,
    // so it only fills in between the nodes that are pulled.
    const double AnchorWeight = 0.01;

    public static bool TryArrange(Node parent, LayoutMetrics metrics, Func<Node, Size> sizeOf)
    {
        var graph = BuildGraph(parent, sizeOf);
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
    static Graph? BuildGraph(Node parent, Func<Node, Size> sizeOf)
    {
        var children = parent
            .Children.Where(child => !child.IsPassThrough && NodeLayout.IsArranged(child))
            .OrderBy(child => child.Name, StringComparer.Ordinal)
            .ToList();
        if (children.Count < 2)
            return null;

        var graphNodes = children.Select(child => new GraphNode(child, sizeOf(child))).ToList();
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
        var cellWidth = graph.Nodes.Average(node => node.Width) + metrics.HorizontalGap;
        var cellHeight = graph.Nodes.Average(node => node.Height) + metrics.VerticalGap;
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
    // sub-columns. Each column is as wide as its widest node, with the nodes centered in it.
    // Vertically, a few sweeps pull each node's center toward the weighted average center of
    // its neighbors in other columns (first the upstream ones, then both sides), keeping the
    // column order and the minimum gaps, so nodes move up as well as down to straighten lines.
    static void AssignCoordinates(Graph graph, LayoutMetrics metrics)
    {
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

        var columnX = NodeGrid.Snap(metrics.HorizontalGap);
        for (var columnIndex = 0; columnIndex < columns.Count; columnIndex++)
        {
            var column = columns[columnIndex];
            var columnWidth = column.Max(node => node.Width);
            graph.Columns.Add(new ColumnExtent(columnX, columnWidth));

            var stackedTop = metrics.VerticalGap;
            foreach (var node in column)
            {
                node.ColumnIndex = columnIndex;
                node.X = NodeGrid.Snap(columnX + (columnWidth - node.Width) / 2);
                node.CenterY = stackedTop + node.Height / 2;
                stackedTop += node.Height + metrics.VerticalGap;
            }

            columnX = NodeGrid.Snap(columnX + columnWidth + metrics.HorizontalGap);
        }

        foreach (var (isLeftToRight, useLeft, useRight) in PlacementSweeps)
        {
            for (var step = 0; step < columns.Count; step++)
            {
                var columnIndex = isLeftToRight ? step : columns.Count - 1 - step;
                PlaceColumn(columns[columnIndex], columnIndex, useLeft, useRight, metrics.VerticalGap);
            }
        }

        SnapColumns(columns, metrics);
    }

    // Order-preserving placement of one column: the node centers as close as possible (weighted
    // least squares) to where their neighbors pull them, keeping the column order and at least
    // the gap between nodes. Substituting z_i = c_i - o_i (o_i the cumulative minimum offset of
    // node i from the first) turns this into isotonic regression, solved by pooling adjacent
    // violators: blocks of nodes that would overlap move together to their weighted mean.
    static void PlaceColumn(List<GraphNode> column, int columnIndex, bool useLeft, bool useRight, double gap)
    {
        var offsets = new double[column.Count];
        for (var index = 1; index < column.Count; index++)
        {
            offsets[index] = offsets[index - 1] + column[index - 1].Height / 2 + gap + column[index].Height / 2;
        }

        var blocks = new List<(double Value, double Weight, int Count)>();
        for (var index = 0; index < column.Count; index++)
        {
            var (desired, weight) = DesiredCenter(column[index], columnIndex, useLeft, useRight);
            var value = desired - offsets[index];
            var count = 1;
            while (blocks.Count > 0 && blocks[^1].Value > value)
            {
                var previous = blocks[^1];
                blocks.RemoveAt(blocks.Count - 1);
                value = (previous.Value * previous.Weight + value * weight) / (previous.Weight + weight);
                weight += previous.Weight;
                count += previous.Count;
            }
            blocks.Add((value, weight, count));
        }

        var nodeIndex = 0;
        foreach (var block in blocks)
        {
            for (var index = 0; index < block.Count; index++, nodeIndex++)
            {
                column[nodeIndex].CenterY = block.Value + offsets[nodeIndex];
            }
        }
    }

    // The weighted average center of the node's neighbors in the columns on the used sides, and
    // the total pull; heavy lines pull harder, damped logarithmically. Without such neighbors the
    // node weakly keeps its current height.
    static (double Center, double Weight) DesiredCenter(GraphNode node, int columnIndex, bool useLeft, bool useRight)
    {
        var weightSum = 0.0;
        var centerSum = 0.0;
        foreach (var (neighbor, weight) in Neighbors(node))
        {
            if (neighbor.ColumnIndex is not int neighborColumn)
                continue;
            var isUsed = (useLeft && neighborColumn < columnIndex) || (useRight && neighborColumn > columnIndex);
            if (!isUsed)
                continue;
            var pull = 1 + Math.Log2(weight);
            weightSum += pull;
            centerSum += neighbor.CenterY * pull;
        }

        return weightSum == 0 ? (node.CenterY, AnchorWeight) : (centerSum / weightSum, weightSum);
    }

    // Snaps the placed nodes to the grid top-down per column, never closer than the gap, then
    // moves the whole content so its top is at the gap (sweeps may have moved nodes upward).
    // Rounds half up, not to even like NodeGrid.Snap: the size steps' half heights all sit at
    // the same offset from the grid, so equal centers then snap alike and lines stay straight.
    static void SnapColumns(List<List<GraphNode>> columns, LayoutMetrics metrics)
    {
        foreach (var column in columns)
        {
            var minTop = double.MinValue;
            foreach (var node in column)
            {
                node.Y = Math.Max(SnapHalfUp(node.CenterY - node.Height / 2), minTop);
                minTop = SnapHalfUp(node.Y + node.Height + metrics.VerticalGap);
            }
        }

        var nodes = columns.SelectMany(column => column).ToList();
        var shift = NodeGrid.Snap(metrics.VerticalGap) - nodes.Min(node => node.Y);
        foreach (var node in nodes)
        {
            node.Y += shift;
            node.Node.Boundary = new Rect(node.X, node.Y, node.Width, node.Height);
        }
    }

    // Nodes without any relations are utility/leftover nodes; pack them as a block at the
    // bottom right of the layered content, largest first, then by name.
    static void PlaceIsolated(Graph graph, LayoutMetrics metrics)
    {
        if (graph.Isolated.Count == 0)
            return;

        var isolated = graph
            .Isolated.OrderByDescending(node => node.Width * node.Height)
            .ThenBy(node => node.Node.Name, StringComparer.Ordinal)
            .ToList();
        var (positions, blockSize) = NodeLayout.PackShelves(
            isolated.Select(node => new Size(node.Width, node.Height)).ToList(),
            targetAspect: 1.0,
            metrics
        );

        var startX =
            graph.Columns.Count == 0
                ? NodeGrid.Snap(metrics.HorizontalGap)
                : NodeGrid.Snap(graph.Columns[^1].X + graph.Columns[^1].Width + metrics.HorizontalGap);
        var contentBottom =
            graph.Nodes.Count == 0 ? 0 : graph.Nodes.Max(node => node.Node.Boundary.Y + node.Node.Boundary.Height);
        var startY = NodeGrid.Snap(Math.Max(metrics.VerticalGap, contentBottom - blockSize.Height));

        for (var index = 0; index < isolated.Count; index++)
        {
            var node = isolated[index];
            node.Node.Boundary = new Rect(
                startX + positions[index].X,
                startY + positions[index].Y,
                node.Width,
                node.Height
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

            line.SetSegmentPoints(RouteSkipEdge(edge, sourceColumn, targetColumn, columns, graph.Columns, metrics));
        }
    }

    static List<Pos> RouteSkipEdge(
        GraphEdge edge,
        int sourceColumn,
        int targetColumn,
        Dictionary<int, List<Rect>> columns,
        IReadOnlyList<ColumnExtent> columnExtents,
        LayoutMetrics metrics
    )
    {
        var goingRight = targetColumn > sourceColumn;
        var sourceRect = edge.Source.Node.Boundary;
        var targetRect = edge.Target.Node.Boundary;

        // The anchors the renderer uses: source leaves from an edge center, target enters one
        var start = new Pos(goingRight ? sourceRect.X + sourceRect.Width : sourceRect.X, CenterY(sourceRect));
        var end = new Pos(goingRight ? targetRect.X : targetRect.X + targetRect.Width, CenterY(targetRect));

        var points = new List<Pos>();
        var step = goingRight ? 1 : -1;

        for (var column = sourceColumn + step; column != targetColumn; column += step)
        {
            if (!columns.TryGetValue(column, out var rects) || rects.Count == 0)
                continue;

            var columnCenterX = columnExtents[column].X + columnExtents[column].Width / 2;
            var straightY = InterpolateY(start, end, columnCenterX);
            if (!CrossesNode(rects, straightY))
                continue;

            var gapY = NearestGapY(rects, straightY, metrics);
            points.Add(new Pos(columnCenterX, gapY));
        }

        return points;
    }

    static double CenterY(Rect rect) => rect.Y + rect.Height / 2;

    static double SnapHalfUp(double value) => Math.Floor(value / NodeGrid.SnapSize + 0.5) * NodeGrid.SnapSize;

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
        public List<ColumnExtent> Columns { get; } = [];
    }

    readonly record struct ColumnExtent(double X, double Width);

    class GraphNode(Node node, Size size)
    {
        public Node Node { get; } = node;
        public double Width { get; } = size.Width;
        public double Height { get; } = size.Height;
        public double X { get; set; }
        public double Y { get; set; }
        public double CenterY { get; set; }
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
