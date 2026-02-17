namespace Dependinator.Models;

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
    const double PlacementPadding = 4;

    public static NodeLayoutDensity Density { get; private set; } = NodeLayoutDensity.Balanced;

    public static void SetDensity(NodeLayoutDensity density) => Density = density;

    public static Rect GetNextChildRect(Node node)
    {
        if (!node.IsRoot)
            return Rect.None;

        var density = GetDensityProfile();
        var rootGap = RootGap * density.GapScale;
        var layoutWidth = node.Boundary.Width / Math.Max(MinimumDimension, node.ContainerZoom);
        var columns = Math.Max(1, (int)Math.Floor((layoutWidth - rootGap) / (DefaultWidth + rootGap)));

        var index = node.Children.Count;
        var column = index % columns;
        var row = index / columns;

        var x = rootGap + column * (DefaultWidth + rootGap);
        var y = rootGap + row * (DefaultHeight + rootGap);
        return new Rect(x, y, DefaultWidth, DefaultHeight);
    }

    public static void AdjustChildren(Node parent, bool forceAllChildren = false)
    {
        parent.IsChildrenLayoutRequired = false;

        if (parent.Children.Count == 0)
            return;

        if (!forceAllChildren && parent.IsChildrenLayoutCustomized)
        {
            ArrangeOnlyNewChildren(parent);
            return;
        }

        if (IsTypeWithMembers(parent))
        {
            ArrangeTypeChildren(parent);
            return;
        }

        Sorter.Sort(parent.Children, CompareChildren);
        var density = GetDensityProfile();
        ArrangeChildren(parent, parent.Children, RegularMetrics(density));
    }

    static bool IsTypeWithMembers(Node parent) =>
        parent.Type == Parsing.NodeType.Type && parent.Children.Any(child => child.Type == Parsing.NodeType.Member);

    static void ArrangeChildren(Node parent, IReadOnlyList<Node> children, LayoutMetrics metrics)
    {
        var columns = FindBestColumnCount(parent, children.Count, metrics);

        for (var index = 0; index < children.Count; index++)
        {
            var column = index % columns;
            var row = index / columns;
            var x = metrics.HorizontalGap + column * (metrics.Width + metrics.HorizontalGap);
            var y = metrics.VerticalGap + row * (metrics.Height + metrics.VerticalGap);
            children[index].Boundary = new Rect(x, y, metrics.Width, metrics.Height);
        }

        ApplyContainerTransform(parent, GetChildrenBounds(children), GetDensityProfile().TargetLinearCoverage);
    }

    static void ArrangeTypeChildren(Node parent)
    {
        var density = GetDensityProfile();
        var memberMetrics = MemberMetrics(density);
        var regularMetrics = RegularMetrics(density);

        var members = parent.Children.Where(c => c.Type == Parsing.NodeType.Member).ToList();
        var nonMembers = parent.Children.Where(c => c.Type != Parsing.NodeType.Member).ToList();

        Sorter.Sort(members, CompareMemberChildren);
        Sorter.Sort(nonMembers, CompareChildren);

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
        _ = ArrangeGroup(parent, nonMembers, regularMetrics, cursorX, startY);

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

        if (IsTypeWithMembers(parent))
        {
            ArrangeOnlyNewTypeChildren(parent, newChildren, occupied, memberMetrics, regularMetrics);
            return;
        }

        Sorter.Sort(newChildren, CompareChildren);
        PlaceNodesInFreeSlots(parent, newChildren, occupied, regularMetrics, regularMetrics.HorizontalGap);
    }

    static void ArrangeOnlyNewTypeChildren(
        Node parent,
        IReadOnlyList<Node> newChildren,
        List<Rect> occupied,
        LayoutMetrics memberMetrics,
        LayoutMetrics regularMetrics
    )
    {
        var newMembers = newChildren.Where(c => c.Type == Parsing.NodeType.Member).ToList();
        var newNonMembers = newChildren.Where(c => c.Type != Parsing.NodeType.Member).ToList();

        Sorter.Sort(newMembers, CompareMemberChildren);
        Sorter.Sort(newNonMembers, CompareChildren);

        var existingMembers = parent
            .Children.Where(c => c.Boundary != Rect.None && c.Type == Parsing.NodeType.Member)
            .ToList();
        var existingPublicMembers = existingMembers
            .Where(m => !(m.IsPrivate ?? false))
            .Select(m => m.Boundary)
            .ToList();
        var existingPrivateMembers = existingMembers.Where(m => m.IsPrivate ?? false).Select(m => m.Boundary).ToList();
        var existingNonMembers = parent
            .Children.Where(c => c.Boundary != Rect.None && c.Type != Parsing.NodeType.Member)
            .Select(c => c.Boundary)
            .ToList();

        var newPublicMembers = newMembers.Where(m => !(m.IsPrivate ?? false)).ToList();
        var newPrivateMembers = newMembers.Where(m => m.IsPrivate ?? false).ToList();

        var minStartX = Math.Max(memberMetrics.HorizontalGap, regularMetrics.HorizontalGap);
        var publicStartX = PreferredStartX(existingPublicMembers, minStartX);
        PlaceNodesInFreeSlots(parent, newPublicMembers, occupied, memberMetrics, publicStartX);

        var publicRight = MaxRight(existingPublicMembers, newPublicMembers.Select(m => m.Boundary));
        var privateStartX = PreferredStartX(
            existingPrivateMembers,
            Math.Max(minStartX, publicRight + memberMetrics.HorizontalGap)
        );
        PlaceNodesInFreeSlots(parent, newPrivateMembers, occupied, memberMetrics, privateStartX);

        var membersRight = MaxRight(
            existingPublicMembers.Concat(existingPrivateMembers),
            newPublicMembers.Concat(newPrivateMembers).Select(m => m.Boundary)
        );
        var nonMemberStartX = PreferredStartX(
            existingNonMembers,
            Math.Max(minStartX, membersRight + regularMetrics.HorizontalGap)
        );
        PlaceNodesInFreeSlots(parent, newNonMembers, occupied, regularMetrics, nonMemberStartX);
    }

    static void PlaceNodesInFreeSlots(
        Node parent,
        IReadOnlyList<Node> nodes,
        List<Rect> occupied,
        LayoutMetrics metrics,
        double preferredStartX
    )
    {
        foreach (var node in nodes)
        {
            var boundary = FindNextFreeRect(parent, occupied, metrics, preferredStartX);
            node.Boundary = boundary;
            occupied.Add(boundary);
        }
    }

    static Rect FindNextFreeRect(
        Node parent,
        IReadOnlyList<Rect> occupied,
        LayoutMetrics metrics,
        double preferredStartX
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
            var candidate = new Rect(x, y, metrics.Width, metrics.Height);
            if (occupied.All(existing => !Overlaps(candidate, existing)))
                return candidate;
        }

        var fallbackX =
            occupied.Count == 0 ? startX : occupied.Max(rect => rect.X + rect.Width) + metrics.HorizontalGap;
        return new Rect(fallbackX, metrics.VerticalGap, metrics.Width, metrics.Height);
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
            nodes[index].Boundary = new Rect(x, y, metrics.Width, metrics.Height);
        }

        return startX + layoutWidth;
    }

    static int FindBestColumnCount(Node parent, int childrenCount, LayoutMetrics metrics)
    {
        if (childrenCount <= 1)
            return 1;

        var parentAspect = parent.Boundary.Height <= 0 ? 1.0 : parent.Boundary.Width / parent.Boundary.Height;
        var targetAspect = Math.Max(0.2, parentAspect * metrics.AspectBias);

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

    static int CompareChildren(Node c1, Node c2)
    {
        // c1->c2
        if (c1.SourceLines.ContainsBy(l => l.Target == c2) && !c2.SourceLines.ContainsBy(l => l.Target == c1))
            return -1;
        // c2->c1
        if (!c1.SourceLines.ContainsBy(l => l.Target == c2) && c2.SourceLines.ContainsBy(l => l.Target == c1))
            return 1;

        // Parent->c1 but not Parent->c2
        if (
            c1.TargetLines.ContainsBy(l => l.Source == c1.Parent)
            && !c2.TargetLines.ContainsBy(l => l.Source == c2.Parent)
        )
            return -1;
        // Not Parent->c1 but Parent->c2
        if (
            !c1.TargetLines.ContainsBy(l => l.Source == c1.Parent)
            && c2.TargetLines.ContainsBy(l => l.Source == c2.Parent)
        )
            return 1;

        // c1->Parent but not c2->Parent
        if (
            c1.SourceLines.ContainsBy(l => l.Target == c1.Parent)
            && !c2.SourceLines.ContainsBy(l => l.Target == c2.Parent)
        )
            return -1;
        // Not c1->Parent but c2->Parent
        if (
            !c1.TargetLines.ContainsBy(l => l.Source == c1.Parent)
            && c2.TargetLines.ContainsBy(l => l.Source == c2.Parent)
        )
            return 1;

        return 0;
    }

    static int CompareMemberChildren(Node c1, Node c2)
    {
        var (v1, v2) = ((int)c1.MemberType, (int)c2.MemberType);
        return v1 < v2 ? -1
            : v2 > v1 ? 1
            : 0;
    }

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
            NodeLayoutDensity.Spacious => new DensityProfile(GapScale: 1.35, TargetLinearCoverage: 0.42),
            NodeLayoutDensity.Compact => new DensityProfile(GapScale: 0.72, TargetLinearCoverage: 0.62),
            _ => new DensityProfile(GapScale: 1.0, TargetLinearCoverage: 0.5),
        };

    readonly record struct LayoutMetrics(
        double Width,
        double Height,
        double HorizontalGap,
        double VerticalGap,
        double AspectBias
    );

    readonly record struct DensityProfile(double GapScale, double TargetLinearCoverage);
}
