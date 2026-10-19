using Dependinator.UI.Shared.Types;
using GraphNode = Dependinator.UI.Modeling.Layout.LayeredEngine.GraphNode;

namespace Dependinator.UI.Modeling.Layout;

// The thorough pass of the layered layout: it keeps the prepared columns (see
// LayeredEngine.Prepare) but searches harder for an order within them with few line crossings,
// and places lines spanning several columns as well as nodes. Such a line gets a dummy point in
// each column it passes, so its crossings count while ordering, space is kept free for it, and
// its waypoints come from where its dummies end up. Ordering repeats barycenter sweeps with
// adjacent swaps (transpose) from a few different starting orders and keeps the order with the
// fewest weighted crossings.
//
// The run is resumable: Step does a limited amount of work, so a background refinement can
// spread the run over idle moments. Its work budget is counted in units of work done (nodes and
// line segments visited), never in time, and checked between whole steps, so the result is the
// same however the run is sliced and on any machine. A plan is used by one run only.
sealed class ThoroughLayoutRun
{
    const int MaxRestarts = 6;
    const int SweepsPerRestart = 12;
    const int MaxTransposeRounds = 4;

    // A restart ends after this many sweeps without fewer crossings
    const int StaleSweepLimit = 2;

    // Vertical placement sweeps, the first along the flow pulled by the upstream side only
    const int PlacementSweeps = 6;

    // Line segments between dummies pull much harder in placement, so long lines run straight;
    // a segment between a node and a dummy a bit harder.
    const double DummySegmentPull = 8;
    const double DummyEndPull = 2;

    readonly LayoutPlan plan;
    readonly LayoutMetrics metrics;
    readonly long workBudget;
    readonly List<List<Item>> columns = [];
    readonly Dictionary<int, List<Item>> dummiesByEdge = [];
    readonly IEnumerator<bool> steps;
    long workDone;
    LayoutResult? result;

    public ThoroughLayoutRun(LayoutPlan plan, long workBudget)
    {
        this.plan = plan;
        metrics = plan.Metrics;
        this.workBudget = workBudget;
        BuildProperGraph();
        steps = Run().GetEnumerator();
    }

    public bool IsComplete => result is not null;
    public long WorkDone => workDone;
    public LayoutResult Result => result ?? throw new InvalidOperationException("The layout run is not complete");

    // A rough count of the work a complete run takes, to decide whether to run it now (inline)
    // or later; it is in the same units as the work budget.
    public static long EstimateWork(LayoutPlan plan)
    {
        long items = plan.Graph.Nodes.Count;
        long segments = 0;
        foreach (var edge in plan.Graph.Edges)
        {
            if (edge.IsHidden || edge.Source.ColumnIndex is not int source || edge.Target.ColumnIndex is not int target)
                continue;
            var span = target - source;
            if (span == 1 || span == -1)
                segments++;
            else if (span > 1 && !edge.IsUserRouted)
            {
                items += span - 1;
                segments += span;
            }
        }

        var perSweep = 3 * (items + segments) + 2 * MaxTransposeRounds * segments;
        return MaxRestarts * SweepsPerRestart * perSweep;
    }

    // Works until the run is complete or at least maxWork more work is done.
    public void Step(long maxWork)
    {
        var stopAt = workDone + maxWork;
        while (result is null && workDone < stopAt && steps.MoveNext()) { }
    }

    public LayoutResult RunToCompletion()
    {
        Step(long.MaxValue);
        return Result;
    }

    bool HasBudget => workDone < workBudget;

    // Each yield ends a whole step of work; Step only decides when to continue.
    IEnumerable<bool> Run()
    {
        var bestCrossings = double.MaxValue;
        List<List<Item>>? bestOrder = null;

        for (var restart = 0; restart < MaxRestarts && HasBudget && bestCrossings > 0; restart++)
        {
            SetStartOrder(restart);
            yield return false;

            var restartBest = CountCrossings();
            if (restartBest < bestCrossings)
                (bestCrossings, bestOrder) = (restartBest, SaveOrder());
            yield return false;

            var staleSweeps = 0;
            for (var sweep = 0; sweep < SweepsPerRestart && staleSweeps < StaleSweepLimit && HasBudget; sweep++)
            {
                for (var column = 1; column < columns.Count && HasBudget; column++)
                {
                    SortByBarycenter(columns[column], toLeft: true);
                    yield return false;
                }
                for (var column = columns.Count - 2; column >= 0 && HasBudget; column--)
                {
                    SortByBarycenter(columns[column], toLeft: false);
                    yield return false;
                }
                for (var round = 0; round < MaxTransposeRounds && HasBudget; round++)
                {
                    var isSwapped = false;
                    foreach (var column in columns)
                        isSwapped |= Transpose(column);
                    yield return false;
                    if (!isSwapped)
                        break;
                }

                var crossings = CountCrossings();
                if (crossings < bestCrossings)
                    (bestCrossings, bestOrder) = (crossings, SaveOrder());
                if (crossings < restartBest)
                    (restartBest, staleSweeps) = (crossings, 0);
                else
                    staleSweeps++;
                yield return false;
            }
        }

        RestoreOrder(bestOrder!);
        result = PlaceAndRoute();
        yield return true;
    }

    // The columns of the prepared plan, plus a dummy item in each column a forward line spans
    // without stopping, and the line segments between items in adjacent columns. Lines within a
    // column (folded layers), hidden lines and long lines the user routed or that point backward
    // are left out of ordering (the latter two are routed after placement).
    void BuildProperGraph()
    {
        foreach (var column in plan.Graph.Columns)
            columns.Add([.. column.Select(node => new Item(node, node.ColumnIndex!.Value))]);
        var itemsByNode = columns.SelectMany(column => column).ToDictionary(item => item.Node!);

        foreach (var edge in plan.Graph.Edges)
        {
            if (edge.IsHidden)
                continue;
            var source = itemsByNode[edge.Source];
            var target = itemsByNode[edge.Target];
            var span = target.Column - source.Column;
            if (span == 1)
                AddSegment(source, target, edge.Weight);
            else if (span == -1)
                AddSegment(target, source, edge.Weight);
            else if (span > 1 && !edge.IsUserRouted)
            {
                var dummies = new List<Item>();
                var previous = source;
                for (var column = source.Column + 1; column < target.Column; column++)
                {
                    var dummy = new Item(null, column);
                    columns[column].Add(dummy);
                    dummies.Add(dummy);
                    AddSegment(previous, dummy, edge.Weight);
                    previous = dummy;
                }
                AddSegment(previous, target, edge.Weight);
                dummiesByEdge[edge.Index] = dummies;
            }
        }
        foreach (var column in columns)
        {
            SetPositions(column);
            column.ForEach(item => item.StartPosition = item.Position);
        }
    }

    static void AddSegment(Item left, Item right, double weight)
    {
        var segment = new Segment(left, right, 1 + Math.Log2(Math.Max(1, weight)));
        left.RightSegments.Add(segment);
        right.LeftSegments.Add(segment);
    }

    // Restart 0 starts from the prepared (fast) order, 1 from its reverse, the others from
    // shuffles with a fixed seed each.
    void SetStartOrder(int restart)
    {
        if (restart == 0)
            return; // The prepared order the run was built with
        var random = new SplitMix64((ulong)restart);
        foreach (var column in columns)
        {
            var ordered =
                restart == 1
                    ? column.OrderByDescending(item => item.StartPosition).ToList()
                    : column.OrderBy(item => item.StartPosition).ToList();
            if (restart > 1)
            {
                for (var index = ordered.Count - 1; index > 0; index--)
                {
                    var other = random.Next(index + 1);
                    (ordered[index], ordered[other]) = (ordered[other], ordered[index]);
                }
            }
            column.Clear();
            column.AddRange(ordered);
            SetPositions(column);
            workDone += column.Count;
        }
    }

    // Orders the column by the weighted average position of each item's neighbors on one side;
    // items without neighbors there keep their position. Moving left to right, the lines
    // entering the container (at its top left) pull their nodes up.
    void SortByBarycenter(List<Item> column, bool toLeft)
    {
        var barycenters = new Dictionary<Item, double>();
        foreach (var item in column)
        {
            var segments = toLeft ? item.LeftSegments : item.RightSegments;
            var weightSum = 0.0;
            var positionSum = 0.0;
            foreach (var segment in segments)
            {
                var neighbor = toLeft ? segment.Left : segment.Right;
                weightSum += segment.Weight;
                positionSum += neighbor.Position * segment.Weight;
            }
            if (toLeft && item.Node is { ExternalIn: > 0 } node)
                weightSum += 1 + Math.Log2(node.ExternalIn);
            barycenters[item] = weightSum == 0 ? item.Position : positionSum / weightSum;
            workDone += 1 + segments.Count;
        }

        var ordered = column.OrderBy(item => barycenters[item]).ThenBy(item => item.Position).ToList();
        column.Clear();
        column.AddRange(ordered);
        SetPositions(column);
    }

    // Swaps adjacent items where that lowers the crossings of their lines on both sides.
    bool Transpose(List<Item> column)
    {
        var isSwapped = false;
        for (var index = 0; index < column.Count - 1; index++)
        {
            var upper = column[index];
            var lower = column[index + 1];
            var current = PairCrossings(upper, lower);
            var swapped = PairCrossings(lower, upper);
            if (swapped >= current)
                continue;

            (column[index], column[index + 1]) = (lower, upper);
            upper.Position = index + 1;
            lower.Position = index;
            isSwapped = true;
        }
        return isSwapped;
    }

    // The weighted crossings between the lines of two items in the given vertical order.
    double PairCrossings(Item upper, Item lower) =>
        PairCrossings(upper.LeftSegments, lower.LeftSegments, segment => segment.Left)
        + PairCrossings(upper.RightSegments, lower.RightSegments, segment => segment.Right);

    double PairCrossings(List<Segment> upper, List<Segment> lower, Func<Segment, Item> neighborOf)
    {
        var crossings = 0.0;
        foreach (var first in upper)
        {
            foreach (var second in lower)
            {
                if (neighborOf(first).Position > neighborOf(second).Position)
                    crossings += first.Weight * second.Weight;
            }
        }
        workDone += 1 + upper.Count * lower.Count;
        return crossings;
    }

    // The weighted crossings between all adjacent columns: per column pair, the inversions of
    // the right ends when the segments are sorted by their left ends (a Fenwick tree of weights).
    double CountCrossings()
    {
        var crossings = 0.0;
        for (var column = 0; column < columns.Count - 1; column++)
        {
            var segments = columns[column]
                .SelectMany(item => item.RightSegments)
                .OrderBy(segment => segment.Left.Position)
                .ThenBy(segment => segment.Right.Position)
                .ToList();
            var tree = new double[columns[column + 1].Count + 1];
            var total = 0.0;
            foreach (var segment in segments)
            {
                var notCrossing = 0.0; // Earlier segments ending at or above this one's right end
                for (var index = segment.Right.Position + 1; index > 0; index -= index & -index)
                    notCrossing += tree[index];
                crossings += segment.Weight * (total - notCrossing);
                for (var index = segment.Right.Position + 1; index < tree.Length; index += index & -index)
                    tree[index] += segment.Weight;
                total += segment.Weight;
            }
            workDone += 1 + segments.Count;
        }
        return crossings;
    }

    static void SetPositions(List<Item> column)
    {
        for (var index = 0; index < column.Count; index++)
            column[index].Position = index;
    }

    List<List<Item>> SaveOrder() => [.. columns.Select(column => column.ToList())];

    void RestoreOrder(List<List<Item>> order)
    {
        for (var column = 0; column < columns.Count; column++)
        {
            columns[column].Clear();
            columns[column].AddRange(order[column]);
            SetPositions(columns[column]);
        }
    }

    LayoutResult PlaceAndRoute()
    {
        var graph = plan.Graph;
        PlaceColumnsHorizontally();
        PlaceVertically();

        // The lines without dummies: within adjacent columns straight, the rest routed around
        // the placed nodes like the fast pass does; then the dummies' lines.
        var waypoints = LayeredEngine.RouteLines(graph, metrics);
        foreach (var (edgeIndex, dummies) in dummiesByEdge)
        {
            var edge = graph.Edges[edgeIndex];
            waypoints[edgeIndex] = Simplify(
                new Pos(edge.Source.Boundary.X + edge.Source.Width, CenterY(edge.Source.Boundary)),
                [.. dummies.Select(dummy => new Pos(DummyX(dummy), dummy.CenterY))],
                new Pos(edge.Target.Boundary.X, CenterY(edge.Target.Boundary))
            );
        }

        LayeredEngine.PlaceIsolated(graph, metrics);
        return plan.ToResult(waypoints);
    }

    // Each column as wide as its widest node, nodes centered in it (as in the fast pass).
    void PlaceColumnsHorizontally()
    {
        var extents = plan.Graph.ColumnExtents;
        extents.Clear();
        var columnX = NodeGrid.Snap(metrics.HorizontalGap);
        foreach (var column in columns)
        {
            var columnWidth = column.Where(item => item.Node is not null).Max(item => item.Node!.Width);
            extents.Add(new LayeredEngine.ColumnExtent(columnX, columnWidth));
            foreach (var item in column)
            {
                if (item.Node is { } node)
                    node.X = NodeGrid.Snap(columnX + (columnWidth - node.Width) / 2);
            }
            columnX = NodeGrid.Snap(columnX + columnWidth + metrics.HorizontalGap);
        }
    }

    double DummyX(Item dummy)
    {
        var extent = plan.Graph.ColumnExtents[dummy.Column];
        return extent.X + extent.Width / 2;
    }

    // Like the fast pass: sweeps pull each item's center toward its neighbors in the adjacent
    // columns, an order-preserving weighted least-squares fit per column; then everything is
    // snapped so node centers and dummies share the grid's center lattice.
    void PlaceVertically()
    {
        foreach (var column in columns)
        {
            var center = metrics.VerticalGap;
            Item? previous = null;
            foreach (var item in column)
            {
                center += previous is null
                    ? item.Height / 2
                    : previous.Height / 2 + Gap(previous, item) + item.Height / 2;
                item.CenterY = center;
                previous = item;
            }
        }

        for (var sweep = 0; sweep < PlacementSweeps; sweep++)
        {
            var isLeftToRight = sweep % 2 == 0;
            for (var step = 0; step < columns.Count; step++)
            {
                var column = columns[isLeftToRight ? step : columns.Count - 1 - step];
                PlaceColumn(column, useLeft: true, useRight: sweep > 0);
            }
        }

        SnapColumns();
    }

    void PlaceColumn(List<Item> column, bool useLeft, bool useRight)
    {
        var offsets = new double[column.Count];
        for (var index = 1; index < column.Count; index++)
        {
            offsets[index] =
                offsets[index - 1]
                + column[index - 1].Height / 2
                + Gap(column[index - 1], column[index])
                + column[index].Height / 2;
        }

        var blocks = new List<(double Value, double Weight, int Count)>();
        for (var index = 0; index < column.Count; index++)
        {
            var (desired, weight) = DesiredCenter(column[index], useLeft, useRight);
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

        var itemIndex = 0;
        foreach (var block in blocks)
        {
            for (var index = 0; index < block.Count; index++, itemIndex++)
                column[itemIndex].CenterY = block.Value + offsets[itemIndex];
        }
    }

    static (double Center, double Weight) DesiredCenter(Item item, bool useLeft, bool useRight)
    {
        var weightSum = 0.0;
        var centerSum = 0.0;
        if (useLeft)
        {
            foreach (var segment in item.LeftSegments)
                Pull(segment, segment.Left);
        }
        if (useRight)
        {
            foreach (var segment in item.RightSegments)
                Pull(segment, segment.Right);
        }
        return weightSum == 0 ? (item.CenterY, LayeredEngine.AnchorWeight) : (centerSum / weightSum, weightSum);

        void Pull(Segment segment, Item neighbor)
        {
            var dummies = (item.IsDummy ? 1 : 0) + (neighbor.IsDummy ? 1 : 0);
            var pull =
                segment.Weight
                * (
                    dummies == 2 ? DummySegmentPull
                    : dummies == 1 ? DummyEndPull
                    : 1
                );
            weightSum += pull;
            centerSum += neighbor.CenterY * pull;
        }
    }

    // Nodes keep the full gap between them, a line passing a node half of it, lines between
    // each other one grid step.
    double Gap(Item upper, Item lower) =>
        upper.IsDummy && lower.IsDummy ? NodeGrid.SnapSize
        : upper.IsDummy || lower.IsDummy ? metrics.VerticalGap / 2
        : metrics.VerticalGap;

    // Top-down per column: node tops on the grid and dummies on the node centers' lattice (the
    // size steps' half heights sit half a grid step off the grid), never closer than the gaps;
    // then all moved so the topmost node sits at the gap.
    void SnapColumns()
    {
        foreach (var column in columns)
        {
            var bottom = double.MinValue / 2; // The previous item's bottom (a dummy's center)
            Item? previous = null;
            foreach (var item in column)
            {
                var minimum = previous is null ? double.MinValue / 2 : bottom + Gap(previous, item);
                if (item.Node is { } node)
                {
                    node.Y = Math.Max(
                        LayeredEngine.SnapHalfUp(item.CenterY - node.Height / 2),
                        LayeredEngine.SnapHalfUp(minimum)
                    );
                    item.CenterY = node.Y + node.Height / 2;
                    bottom = node.Y + node.Height;
                }
                else
                {
                    item.CenterY = Math.Max(SnapToCenterLattice(item.CenterY), SnapToCenterLattice(minimum));
                    bottom = item.CenterY;
                }
                previous = item;
            }
        }

        var nodes = columns.SelectMany(column => column).Where(item => item.Node is not null).ToList();
        var shift = NodeGrid.Snap(metrics.VerticalGap) - nodes.Min(item => item.Node!.Y);
        foreach (var item in columns.SelectMany(column => column))
        {
            item.CenterY += shift;
            if (item.Node is { } node)
            {
                node.Y += shift;
                node.Boundary = new Rect(node.X, node.Y, node.Width, node.Height);
            }
        }
    }

    static double SnapToCenterLattice(double value) =>
        LayeredEngine.SnapHalfUp(value - NodeGrid.SnapSize / 2) + NodeGrid.SnapSize / 2;

    static double CenterY(Rect rect) => rect.Y + rect.Height / 2;

    // The waypoints that bend the line: points on a straight run between their neighbors drop.
    static List<Pos> Simplify(Pos start, List<Pos> points, Pos end)
    {
        var kept = new List<Pos>();
        var previous = start;
        for (var index = 0; index < points.Count; index++)
        {
            var next = index + 1 < points.Count ? points[index + 1] : end;
            var cross =
                (points[index].X - previous.X) * (next.Y - previous.Y)
                - (points[index].Y - previous.Y) * (next.X - previous.X);
            if (Math.Abs(cross) < 0.001)
                continue;
            kept.Add(points[index]);
            previous = points[index];
        }
        return kept;
    }

    // A node of the plan, or a dummy point of a long line passing the column.
    sealed class Item(GraphNode? node, int column)
    {
        public GraphNode? Node { get; } = node;
        public int Column { get; } = column;
        public bool IsDummy => Node is null;
        public double Height => Node?.Height ?? 0;
        public int Position { get; set; }
        public int StartPosition { get; set; }
        public double CenterY { get; set; }
        public List<Segment> LeftSegments { get; } = [];
        public List<Segment> RightSegments { get; } = [];
    }

    sealed record Segment(Item Left, Item Right, double Weight);

    // A small, fixed pseudo-random sequence (SplitMix64), so shuffled restarts are the same on
    // every platform and .NET version.
    struct SplitMix64(ulong seed)
    {
        ulong state = seed;

        public int Next(int maxExclusive)
        {
            state += 0x9E3779B97F4A7C15;
            var z = state;
            z = (z ^ (z >> 30)) * 0xBF58476D1CE4E5B9;
            z = (z ^ (z >> 27)) * 0x94D049BB133111EB;
            z ^= z >> 31;
            return (int)(z % (ulong)maxExclusive);
        }
    }
}
