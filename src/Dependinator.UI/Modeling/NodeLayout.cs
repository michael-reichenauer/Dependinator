using Dependinator.UI.Modeling.Layout;
using Dependinator.UI.Modeling.Models;
using Dependinator.UI.Shared.Types;
using NodeType = Dependinator.Core.Parsing.NodeType;

namespace Dependinator.UI.Modeling;

enum NodeLayoutDensity
{
    Spacious,
    Balanced,
    Compact,
}

class NodeLayout
{
    const double DefaultWidth = 100;
    const double DefaultHeight = 100;
    public static readonly Size DefaultSize = new(DefaultWidth, DefaultHeight);
    const double RegularHorizontalGap = 70;
    const double RegularVerticalGap = 70;
    const double RootGap = 10;
    const double MemberNodeWidth = 100;
    const double MemberNodeHeight = 18;
    const double MemberHorizontalGap = 26;
    const double MemberVerticalGap = 6;
    const double MemberAspectBias = 0.45;
    const double EmptyCellPenaltyWeight = 0.15;
    const double MinimumZoom = 1.0 / 40.0;
    const double MaximumZoom = 1.0;
    const double MinimumDimension = 0.0001;
    const int PlacementScanLimit = 5000;

    // Work budgets of the thorough layered layout pass (see ThoroughLayoutRun): containers whose
    // layout is estimated to fit get it right away when first shown, others the fast pass; a
    // reset, which the user asked for, may take more.
    const long InlineThoroughWorkBudget = 250_000;
    const long ResetThoroughWorkBudget = 2_000_000;
    const double PlacementPadding = 4;
    static readonly DensityProfile SpaciousProfile = new(GapScale: 1.35, TargetLinearCoverage: 0.42);
    static readonly DensityProfile CompactProfile = new(GapScale: 0.72, TargetLinearCoverage: 0.62);
    static readonly DensityProfile BalancedProfile = new(GapScale: 1.0, TargetLinearCoverage: 0.5);
    static readonly DensityProfile RootProfile = new(GapScale: 5.00, TargetLinearCoverage: 0.42);

    // Bumped when the automatic layout changes enough that existing automatic layouts should be
    // redone: a model saved with an older version gets its containers laid out again when
    // loaded, except those the user arranged (see ModelService.LoadCachedModelDataAsync).
    // 1: sizes by code and centrality (NodeSizing), straighter lines.
    public const int CurrentLayoutVersion = 1;

    public static NodeLayoutDensity Density { get; private set; } = NodeLayoutDensity.Balanced;

    // Bumped whenever node geometry changes (layout, container fit, node edits), so per-zoom
    // derived state such as the line representatives (RepLineService) knows that nodes may have
    // crossed an open/icon threshold although the zoom stayed the same (see NodeViewPolicy:
    // the threshold depends on the node's size). Process-wide: with several Blazor Server
    // circuits a bump only costs the others one extra check.
    static long generation;

    public static long Generation => Interlocked.Read(ref generation);

    public static void BumpGeneration() => Interlocked.Increment(ref generation);

    public static void SetDensity(NodeLayoutDensity density) => Density = density;

    static double SnapToGrid(double value) => Math.Round(value / NodeGrid.SnapSize) * NodeGrid.SnapSize;

    public static Rect SnapPositionToGrid(Rect rect) => rect with { X = SnapToGrid(rect.X), Y = SnapToGrid(rect.Y) };

    // "Reset Children Layout": hands the container back to the automatic layout. Clears the
    // customized flag, so later re-parses lay the container out fully again instead of only
    // placing new children, and the user's waypoints on the lines drawn inside it, which were
    // placed for the old positions; then lays out all children. Recursion runs top-down, since a
    // parent's layout sizes a child before the child lays out its own children.
    public static void ResetChildrenLayout(Node node, bool recursively)
    {
        node.IsChildrenLayoutCustomized = false;
        foreach (var line in LinesDrawnInside(node).Where(line => line.IsSegmentsUserSet))
        {
            line.IsSegmentsUserSet = false;
            line.SetSegmentPoints([]);
        }

        AdjustChildren(node, forceAllChildren: true);

        if (recursively)
            node.Children.ForEach(child => ResetChildrenLayout(child, recursively));
    }

    // The lines rendered in the container's coordinate space: the fan-out from the container
    // to its children, the children's sibling and child-to-container lines, and the direct
    // lines the container renders (see SvgService.DrawnNodeLines).
    internal static IEnumerable<Line> LinesDrawnInside(Node container) =>
        container
            .SourceLines.Where(line => line.Target.Parent == container)
            .Concat(
                container.Children.SelectMany(child =>
                    child.SourceLines.Where(line => line.Target.Parent != line.Source)
                )
            )
            .Concat(container.DirectLines);

    public static void AdjustChildren(Node parent, bool forceAllChildren = false)
    {
        parent.IsChildrenLayoutRequired = false;
        BumpGeneration();

        if (parent.Children.Count == 0)
            return;

        // A sole pass-through child has a derived boundary that always covers the parent's
        // viewport; arranging it or fitting the container transform to it would fight that.
        if (parent.Children.Count == 1 && parent.Children[0].IsPassThrough)
            return;

        if (!forceAllChildren && parent.IsChildrenLayoutCustomized)
        {
            ArrangeOnlyNewChildren(parent);
            return;
        }

        var sizesBefore = CaptureSizes(parent.Children);

        if (HasMemberChildren(parent))
            ArrangeTypeChildren(parent);
        else
            ArrangeChildren(parent, forceAllChildren ? ResetThoroughWorkBudget : InlineThoroughWorkBudget);

        RescaleResizedContent(sizesBefore);
    }

    static void ArrangeChildren(Node parent, long thoroughWorkBudget)
    {
        var density = GetDensityProfile();
        var metrics = parent.IsRoot ? RootMetrics() : RegularMetrics(density);
        var sizeOf = ChildSizes(parent, SizedChildren(parent.Children), metrics);

        if (!LayeredLayout.TryArrange(parent, metrics, sizeOf, thoroughWorkBudget, out _))
        {
            SortChildren(parent.Children);
            ArrangeAsShelves(
                parent.Children.Where(IsArranged).ToList(),
                sizeOf,
                metrics,
                TargetAspect(parent, metrics),
                metrics.HorizontalGap,
                metrics.VerticalGap
            );
        }

        // The root's container transform is the fixed identity-like frame the canvas viewport
        // (model.Zoom/Offset) pans and zooms within, so it must not be re-fitted by layout.
        if (!parent.IsRoot)
            ApplyContainerTransform(parent, GetChildrenBounds(parent.Children), density.TargetLinearCoverage);
    }

    // Members (C# type members, Bicep params/vars/outputs) are laid out as rows; a C# type is the
    // usual such parent, but a Bicep file or module holds member rows next to resources too.
    static bool HasMemberChildren(Node parent) => parent.Children.Any(child => child.Type.IsMember);

    // Zones and notes stay where the user put them: a layout (also a forced "Reset Children
    // Layout") arranges the other children around them. They still count for the container fit.
    internal static bool IsArranged(Node child) => !child.IsZone && !child.IsNote;

    // The children that get a node size of their own (see NodeSizing): not member rows, zones,
    // notes or pass-through nodes (whose boundary is derived from the parent).
    static IReadOnlyList<Node> SizedChildren(IEnumerable<Node> children) =>
        children.Where(child => IsArranged(child) && !child.Type.IsMember && !child.IsPassThrough).ToList();

    // The size of each arranged (non-member) child: its size step among the sized siblings,
    // or the metrics' default size for a child without one.
    static Func<Node, Size> ChildSizes(Node parent, IReadOnlyList<Node> sizedChildren, LayoutMetrics metrics)
    {
        var sides = NodeSizing.ComputeSides(sizedChildren, parent);
        var defaultSize = new Size(metrics.Width, metrics.Height);
        return node => sides.TryGetValue(node, out var side) ? new Size(side, side) : defaultSize;
    }

    static List<(Node Child, Size Size)> CaptureSizes(IEnumerable<Node> children) =>
        children
            .Where(child => !child.IsPassThrough && child.Boundary != Rect.None)
            .Select(child => (child, new Size(child.Boundary.Width, child.Boundary.Height)))
            .ToList();

    // A layout may resize children (see NodeSizing). A resized child whose own children are
    // already laid out keeps their look: its content transform is scaled by the size change and
    // the content re-centered, so e.g. the user's arrangement inside a node survives a reset of
    // the node's parent. With a pass-through chain the deepest chain node holds the content
    // transform (see PassThroughService); the chain's derived boundaries follow the resize.
    static void RescaleResizedContent(IReadOnlyList<(Node Child, Size Size)> sizesBefore)
    {
        foreach (var (child, before) in sizesBefore)
        {
            var after = child.Boundary;
            if (after.Width == before.Width && after.Height == before.Height)
                continue;
            if (before.Width <= 0 || before.Height <= 0)
                continue;

            var container = child;
            while (container.Children.Count == 1 && container.Children[0].IsPassThrough)
                container = container.Children[0];
            if (container.Children.Count == 0 || container.IsChildrenLayoutRequired)
                continue;

            var scale = Math.Min(after.Width / before.Width, after.Height / before.Height);
            var offset = container.ContainerOffset;
            container.ContainerZoom *= scale;
            container.ContainerOffset = new Pos(
                offset.X * scale + (after.Width - before.Width * scale) / 2,
                offset.Y * scale + (after.Height - before.Height * scale) / 2
            );
        }
    }

    // Arranges the nodes as shelf-packed rows from (startX, startY), largest first; equally
    // large nodes keep their order (a stable sort). Returns the right edge of the block.
    static double ArrangeAsShelves(
        IReadOnlyList<Node> nodes,
        Func<Node, Size> sizeOf,
        LayoutMetrics metrics,
        double targetAspect,
        double startX,
        double startY
    )
    {
        if (nodes.Count == 0)
            return startX;

        var ordered = nodes.OrderByDescending(node => sizeOf(node).Width * sizeOf(node).Height).ToList();
        var sizes = ordered.Select(sizeOf).ToList();
        var (positions, blockSize) = PackShelves(sizes, targetAspect, metrics);

        var x = NodeGrid.Snap(startX);
        var y = NodeGrid.Snap(startY);
        for (var index = 0; index < ordered.Count; index++)
        {
            ordered[index].Boundary = new Rect(
                x + positions[index].X,
                y + positions[index].Y,
                sizes[index].Width,
                sizes[index].Height
            );
        }
        return x + blockSize.Width;
    }

    // Packs boxes in the given order into rows ("shelves") separated by the metrics' gaps, each
    // box centered vertically in its row. Tries every row width that ends at a box and keeps the
    // packing whose block best matches the target aspect with the least empty area (the same
    // trade-off as FindBestColumnCount). Positions are offsets from the block's top-left corner,
    // on the snap grid.
    internal static (IReadOnlyList<Pos> Positions, Size BlockSize) PackShelves(
        IReadOnlyList<Size> sizes,
        double targetAspect,
        LayoutMetrics metrics
    )
    {
        var gapX = NodeGrid.Snap(metrics.HorizontalGap);
        var gapY = NodeGrid.Snap(metrics.VerticalGap);
        var occupiedArea = sizes.Sum(size => size.Width * size.Height);

        IReadOnlyList<Pos> bestPositions = [];
        var bestSize = new Size(0, 0);
        var bestScore = double.MaxValue;
        var rowWidthLimit = -gapX;
        foreach (var size in sizes)
        {
            rowWidthLimit += gapX + size.Width;
            var (positions, blockSize) = PackShelvesWithin(sizes, rowWidthLimit, gapX, gapY);

            var aspect = blockSize.Width / Math.Max(MinimumDimension, blockSize.Height);
            var emptyRatio = 1 - occupiedArea / Math.Max(MinimumDimension, blockSize.Width * blockSize.Height);
            var score = Math.Abs(Math.Log(aspect / targetAspect)) + EmptyCellPenaltyWeight * emptyRatio;
            if (score >= bestScore)
                continue;

            bestScore = score;
            bestPositions = positions;
            bestSize = blockSize;
        }
        return (bestPositions, bestSize);
    }

    static (IReadOnlyList<Pos> Positions, Size BlockSize) PackShelvesWithin(
        IReadOnlyList<Size> sizes,
        double rowWidthLimit,
        double gapX,
        double gapY
    )
    {
        var positions = new Pos[sizes.Count];
        var xs = new double[sizes.Count];
        var rowStart = 0;
        var x = 0.0;
        var rowTop = 0.0;
        var rowHeight = 0.0;
        var blockWidth = 0.0;

        void FinishRow(int rowEnd)
        {
            for (var index = rowStart; index < rowEnd; index++)
            {
                var y = rowTop + (rowHeight - sizes[index].Height) / 2;
                positions[index] = new Pos(NodeGrid.Snap(xs[index]), NodeGrid.Snap(y));
            }
        }

        for (var index = 0; index < sizes.Count; index++)
        {
            var size = sizes[index];
            if (index > rowStart && x + size.Width > rowWidthLimit + MinimumDimension)
            {
                FinishRow(index);
                rowTop += rowHeight + gapY;
                rowStart = index;
                x = 0;
                rowHeight = 0;
            }

            xs[index] = x;
            blockWidth = Math.Max(blockWidth, x + size.Width);
            rowHeight = Math.Max(rowHeight, size.Height);
            x += size.Width + gapX;
        }
        FinishRow(sizes.Count);

        return (positions, new Size(blockWidth, rowTop + rowHeight));
    }

    static void ArrangeTypeChildren(Node parent)
    {
        var density = GetDensityProfile();
        var memberMetrics = MemberMetrics(density);
        var regularMetrics = RegularMetrics(density);

        var members = parent.Children.Where(c => c.Type.IsMember).ToList();
        var nonMembers = parent.Children.Where(c => !c.Type.IsMember && IsArranged(c)).ToList();

        Sorter.Sort(members, CompareMemberChildren);
        SortChildren(nonMembers);

        var publicMembers = members.Where(m => !(m.IsPrivate ?? false)).ToList();
        var privateMembers = members.Where(m => m.IsPrivate ?? false).ToList();

        var startY = Math.Max(memberMetrics.VerticalGap, regularMetrics.VerticalGap);
        var cursorX = Math.Max(memberMetrics.HorizontalGap, regularMetrics.HorizontalGap);

        cursorX = ArrangeGroup(parent, publicMembers, memberMetrics, cursorX, startY);
        if (publicMembers.Count > 0 && privateMembers.Count > 0)
            cursorX += memberMetrics.HorizontalGap;
        cursorX = ArrangeGroup(parent, privateMembers, memberMetrics, cursorX, startY);
        if (members.Count > 0 && nonMembers.Count > 0)
            cursorX += regularMetrics.HorizontalGap;
        _ = ArrangeAsShelves(
            nonMembers,
            ChildSizes(parent, SizedChildren(nonMembers), regularMetrics),
            regularMetrics,
            TargetAspect(parent, regularMetrics),
            cursorX + regularMetrics.HorizontalGap,
            startY + regularMetrics.VerticalGap
        );

        ApplyContainerTransform(parent, GetChildrenBounds(parent.Children), density.TargetLinearCoverage);
    }

    static void ArrangeOnlyNewChildren(Node parent)
    {
        var newChildren = parent.Children.Where(child => child.Boundary == Rect.None).ToList();
        if (newChildren.Count == 0)
            return;

        var density = GetDensityProfile();
        var memberMetrics = MemberMetrics(density);
        var regularMetrics = RegularMetrics(density);
        var occupied = parent
            .Children.Where(child => child.Boundary != Rect.None)
            .Select(child => child.Boundary)
            .ToList();

        if (HasMemberChildren(parent))
        {
            ArrangeOnlyNewTypeChildren(parent, newChildren, occupied, memberMetrics, regularMetrics);
            return;
        }

        SortChildren(newChildren);
        PlaceNodesInFreeSlots(
            parent,
            newChildren,
            ChildSizes(parent, SizedChildren(parent.Children), regularMetrics),
            occupied,
            regularMetrics,
            regularMetrics.HorizontalGap
        );
    }

    static void ArrangeOnlyNewTypeChildren(
        Node parent,
        IReadOnlyList<Node> newChildren,
        List<Rect> occupied,
        LayoutMetrics memberMetrics,
        LayoutMetrics regularMetrics
    )
    {
        var newMembers = newChildren.Where(c => c.Type.IsMember).ToList();
        var newNonMembers = newChildren.Where(c => !c.Type.IsMember).ToList();

        Sorter.Sort(newMembers, CompareMemberChildren);
        SortChildren(newNonMembers);

        var existingMembers = parent.Children.Where(c => c.Boundary != Rect.None && c.Type.IsMember).ToList();
        var existingPublicMembers = existingMembers
            .Where(m => !(m.IsPrivate ?? false))
            .Select(m => m.Boundary)
            .ToList();
        var existingPrivateMembers = existingMembers.Where(m => m.IsPrivate ?? false).Select(m => m.Boundary).ToList();
        var existingNonMembers = parent
            .Children.Where(c => c.Boundary != Rect.None && !c.Type.IsMember)
            .Select(c => c.Boundary)
            .ToList();

        var newPublicMembers = newMembers.Where(m => !(m.IsPrivate ?? false)).ToList();
        var newPrivateMembers = newMembers.Where(m => m.IsPrivate ?? false).ToList();

        var minStartX = Math.Max(memberMetrics.HorizontalGap, regularMetrics.HorizontalGap);
        var publicStartX = PreferredStartX(existingPublicMembers, minStartX);
        var memberSize = new Size(memberMetrics.Width, memberMetrics.Height);
        PlaceNodesInFreeSlots(parent, newPublicMembers, _ => memberSize, occupied, memberMetrics, publicStartX);

        var publicRight = MaxRight(existingPublicMembers, newPublicMembers.Select(m => m.Boundary));
        var privateStartX = PreferredStartX(
            existingPrivateMembers,
            Math.Max(minStartX, publicRight + memberMetrics.HorizontalGap)
        );
        PlaceNodesInFreeSlots(parent, newPrivateMembers, _ => memberSize, occupied, memberMetrics, privateStartX);

        var membersRight = MaxRight(
            existingPublicMembers.Concat(existingPrivateMembers),
            newPublicMembers.Concat(newPrivateMembers).Select(m => m.Boundary)
        );
        var nonMemberStartX = PreferredStartX(
            existingNonMembers,
            Math.Max(minStartX, membersRight + regularMetrics.HorizontalGap)
        );
        PlaceNodesInFreeSlots(
            parent,
            newNonMembers,
            ChildSizes(parent, SizedChildren(parent.Children), regularMetrics),
            occupied,
            regularMetrics,
            nonMemberStartX
        );
    }

    static void PlaceNodesInFreeSlots(
        Node parent,
        IReadOnlyList<Node> nodes,
        Func<Node, Size> sizeOf,
        List<Rect> occupied,
        LayoutMetrics metrics,
        double preferredStartX
    )
    {
        foreach (var node in nodes)
        {
            var boundary = FindNextFreeRect(parent, occupied, metrics, preferredStartX, sizeOf(node));
            node.Boundary = boundary;
            occupied.Add(boundary);
        }
    }

    // The first cell of a row-major scan (cells of the metrics' size) where a node of the given
    // size fits without overlapping the occupied rects.
    static Rect FindNextFreeRect(
        Node parent,
        IReadOnlyList<Rect> occupied,
        LayoutMetrics metrics,
        double preferredStartX,
        Size size
    )
    {
        var startX = Math.Max(metrics.HorizontalGap, preferredStartX);
        var layoutWidth = parent.Boundary.Width / Math.Max(MinimumDimension, parent.ContainerZoom);
        var horizontalSpan = Math.Max(metrics.Width, layoutWidth - startX);
        var columns = Math.Max(1, (int)Math.Floor(horizontalSpan / (metrics.Width + metrics.HorizontalGap)));

        for (var index = 0; index < PlacementScanLimit; index++)
        {
            var column = index % columns;
            var row = index / columns;
            var x = startX + column * (metrics.Width + metrics.HorizontalGap);
            var y = metrics.VerticalGap + row * (metrics.Height + metrics.VerticalGap);
            var candidate = SnapPositionToGrid(new Rect(x, y, size.Width, size.Height));
            if (occupied.All(existing => !Overlaps(candidate, existing)))
                return candidate;
        }

        var fallbackX =
            occupied.Count == 0 ? startX : occupied.Max(rect => rect.X + rect.Width) + metrics.HorizontalGap;
        return SnapPositionToGrid(new Rect(fallbackX, metrics.VerticalGap, size.Width, size.Height));
    }

    static bool Overlaps(Rect first, Rect second)
    {
        var x1 = first.X - PlacementPadding;
        var y1 = first.Y - PlacementPadding;
        var x2 = first.X + first.Width + PlacementPadding;
        var y2 = first.Y + first.Height + PlacementPadding;

        var sx1 = second.X - PlacementPadding;
        var sy1 = second.Y - PlacementPadding;
        var sx2 = second.X + second.Width + PlacementPadding;
        var sy2 = second.Y + second.Height + PlacementPadding;

        return x1 < sx2 && x2 > sx1 && y1 < sy2 && y2 > sy1;
    }

    static double PreferredStartX(IEnumerable<Rect> existing, double fallback)
    {
        var list = existing.ToList();
        return list.Count == 0 ? fallback : list.Min(rect => rect.X);
    }

    static double MaxRight(IEnumerable<Rect> existing, IEnumerable<Rect> added)
    {
        var all = existing.Concat(added).ToList();
        return all.Count == 0 ? 0 : all.Max(rect => rect.X + rect.Width);
    }

    static double ArrangeGroup(
        Node parent,
        IReadOnlyList<Node> nodes,
        LayoutMetrics metrics,
        double startX,
        double startY
    )
    {
        if (nodes.Count == 0)
            return startX;

        var columns = FindBestColumnCount(parent, nodes.Count, metrics);
        var (layoutWidth, _) = LayoutSize(columns, (int)Math.Ceiling(nodes.Count / (double)columns), metrics);

        for (var index = 0; index < nodes.Count; index++)
        {
            var column = index % columns;
            var row = index / columns;
            var x = startX + metrics.HorizontalGap + column * (metrics.Width + metrics.HorizontalGap);
            var y = startY + metrics.VerticalGap + row * (metrics.Height + metrics.VerticalGap);
            nodes[index].Boundary = SnapPositionToGrid(new Rect(x, y, metrics.Width, metrics.Height));
        }

        return startX + layoutWidth;
    }

    static int FindBestColumnCount(Node parent, int childrenCount, LayoutMetrics metrics)
    {
        if (childrenCount <= 1)
            return 1;

        var targetAspect = TargetAspect(parent, metrics);

        var bestColumns = 1;
        var bestScore = double.MaxValue;

        for (var columns = 1; columns <= childrenCount; columns++)
        {
            var rows = (int)Math.Ceiling(childrenCount / (double)columns);
            var (layoutWidth, layoutHeight) = LayoutSize(columns, rows, metrics);
            var gridAspect = layoutWidth / Math.Max(MinimumDimension, layoutHeight);

            var aspectError = Math.Abs(Math.Log(gridAspect / targetAspect));
            var emptyCells = columns * rows - childrenCount;
            var emptyCellPenalty = emptyCells / (double)childrenCount;
            var score = aspectError + EmptyCellPenaltyWeight * emptyCellPenalty;

            if (score >= bestScore)
                continue;

            bestScore = score;
            bestColumns = columns;
        }
        return bestColumns;
    }

    // The aspect ratio a block of children should have to fill the parent well.
    static double TargetAspect(Node parent, LayoutMetrics metrics)
    {
        var parentAspect = parent.Boundary.Height <= 0 ? 1.0 : parent.Boundary.Width / parent.Boundary.Height;
        return Math.Max(0.2, parentAspect * metrics.AspectBias);
    }

    static (double Width, double Height) LayoutSize(int columns, int rows, LayoutMetrics metrics)
    {
        var width = columns * metrics.Width + (columns + 1) * metrics.HorizontalGap;
        var height = rows * metrics.Height + (rows + 1) * metrics.VerticalGap;
        return (width, height);
    }

    static Rect GetChildrenBounds(IReadOnlyList<Node> children)
    {
        var first = children[0].Boundary;
        var minX = first.X;
        var minY = first.Y;
        var maxX = first.X + first.Width;
        var maxY = first.Y + first.Height;

        for (var index = 1; index < children.Count; index++)
        {
            var boundary = children[index].Boundary;
            minX = Math.Min(minX, boundary.X);
            minY = Math.Min(minY, boundary.Y);
            maxX = Math.Max(maxX, boundary.X + boundary.Width);
            maxY = Math.Max(maxY, boundary.Y + boundary.Height);
        }

        return new Rect(minX, minY, maxX - minX, maxY - minY);
    }

    // Re-fits the container zoom/offset to the current children without re-arranging them,
    // e.g. when a node's pass-through state changes and its effective boundary jumps in size.
    public static void FitContainerTransform(Node parent)
    {
        BumpGeneration();
        var children = parent.Children.Where(child => child.Boundary != Rect.None).ToList();
        if (children.Count == 0)
            return;

        ApplyContainerTransform(parent, GetChildrenBounds(children), GetDensityProfile().TargetLinearCoverage);
    }

    static void ApplyContainerTransform(Node parent, Rect contentBounds, double targetLinearCoverage)
    {
        var availableWidth = Math.Max(MinimumDimension, parent.Boundary.Width);
        var availableHeight = Math.Max(MinimumDimension, parent.Boundary.Height);
        var contentWidth = Math.Max(MinimumDimension, contentBounds.Width);
        var contentHeight = Math.Max(MinimumDimension, contentBounds.Height);

        var zoomX = (availableWidth * targetLinearCoverage) / contentWidth;
        var zoomY = (availableHeight * targetLinearCoverage) / contentHeight;
        var zoom = Math.Clamp(Math.Min(zoomX, zoomY), MinimumZoom, MaximumZoom);

        var offsetX = (availableWidth - contentWidth * zoom) / 2 - contentBounds.X * zoom;
        var offsetY = (availableHeight - contentHeight * zoom) / 2 - contentBounds.Y * zoom;

        parent.ContainerZoom = zoom;
        parent.ContainerOffset = new Pos(offsetX, offsetY);
    }

    // Orders siblings so referencing children come before the children they reference: first by
    // sibling lines (c1->c2 before c2->c1), then children referenced by the parent, then children
    // referencing the parent. The relation is a partial order, so ties are left in place.
    static void SortChildren(IList<Node> children)
    {
        if (children.Count < 2)
            return;

        // Precompute the line relations once; the comparer runs O(n²) times in Sorter.Sort, so
        // scanning the line lists in every comparison gets expensive for large namespaces.
        var relations = children.ToDictionary(
            child => child,
            child =>
                (
                    SiblingTargets: child.SourceLines.Select(line => line.Target).ToHashSet(),
                    HasLineFromParent: child.TargetLines.ContainsBy(line => line.Source == child.Parent),
                    HasLineToParent: child.SourceLines.ContainsBy(line => line.Target == child.Parent)
                )
        );

        Sorter.Sort(
            children,
            (c1, c2) =>
            {
                var r1 = relations[c1];
                var r2 = relations[c2];

                var c1ToC2 = r1.SiblingTargets.Contains(c2);
                var c2ToC1 = r2.SiblingTargets.Contains(c1);
                if (c1ToC2 != c2ToC1)
                    return c1ToC2 ? -1 : 1;

                if (r1.HasLineFromParent != r2.HasLineFromParent)
                    return r1.HasLineFromParent ? -1 : 1;

                if (r1.HasLineToParent != r2.HasLineToParent)
                    return r1.HasLineToParent ? -1 : 1;

                return 0;
            }
        );
    }

    // Most used/using members first, then by member kind, so the most relevant members end up
    // top left within their public/private group.
    static int CompareMemberChildren(Node c1, Node c2)
    {
        var importance1 = c1.SourceLinks.Count + c1.TargetLinks.Count;
        var importance2 = c2.SourceLinks.Count + c2.TargetLinks.Count;
        if (importance1 != importance2)
            return importance2 - importance1;

        var rank1 = MemberKindRank(c1.Type);
        var rank2 = MemberKindRank(c2.Type);
        if (rank1 != rank2)
            return rank1 - rank2;

        return string.CompareOrdinal(c1.Name, c2.Name);
    }

    static int MemberKindRank(NodeType type) =>
        type switch
        {
            NodeType.ConstructorMember => 0,
            NodeType.PropertyMember => 1,
            NodeType.MethodMember => 2,
            NodeType.EventMember => 3,
            NodeType.FieldMember => 4,
            NodeType.ResourceMember => 5,
            NodeType.ParameterMember => 6,
            NodeType.VariableMember => 7,
            NodeType.OutputMember => 8,
            _ => 9,
        };

    static LayoutMetrics RootMetrics() =>
        new(DefaultWidth, DefaultHeight, RootGap * RootProfile.GapScale, RootGap * RootProfile.GapScale, 1.0);

    static LayoutMetrics RegularMetrics(DensityProfile density) =>
        new(
            DefaultWidth,
            DefaultHeight,
            RegularHorizontalGap * density.GapScale,
            RegularVerticalGap * density.GapScale,
            1.0
        );

    static LayoutMetrics MemberMetrics(DensityProfile density) =>
        new(
            MemberNodeWidth,
            MemberNodeHeight,
            MemberHorizontalGap * density.GapScale,
            MemberVerticalGap * density.GapScale,
            MemberAspectBias
        );

    static DensityProfile GetDensityProfile() =>
        Density switch
        {
            NodeLayoutDensity.Spacious => SpaciousProfile,
            NodeLayoutDensity.Compact => CompactProfile,
            _ => BalancedProfile,
        };

    readonly record struct DensityProfile(double GapScale, double TargetLinearCoverage);
}

readonly record struct LayoutMetrics(
    double Width,
    double Height,
    double HorizontalGap,
    double VerticalGap,
    double AspectBias
);
