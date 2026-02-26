using Dependinator.Models;
using Dependinator.Core.Parsing;
using ModelNode = Dependinator.Models.Node;

namespace Dependinator.Tests.Models;

public class NodeLayoutTests
{
    [Fact]
    public void AdjustChildren_ShouldResetBoundsAndKeepChildrenWithinQuarterArea()
    {
        NodeLayout.SetDensity(NodeLayoutDensity.Balanced);

        var root = new ModelNode("", null!) { Type = NodeType.Root };
        var parent = new ModelNode("Parent", root) { Type = NodeType.Namespace, Boundary = new Rect(0, 0, 1200, 800) };
        root.AddChild(parent);

        for (var i = 0; i < 12; i++)
        {
            var child = new ModelNode($"Child{i}", parent)
            {
                Type = NodeType.Type,
                Boundary = new Rect(i, i, 10 + i, 12 + i),
            };
            parent.AddChild(child);
        }

        NodeLayout.AdjustChildren(parent);

        Assert.False(parent.IsChildrenLayoutRequired);
        Assert.All(
            parent.Children,
            child =>
            {
                Assert.Equal(NodeLayout.DefaultSize.Width, child.Boundary.Width);
                Assert.Equal(NodeLayout.DefaultSize.Height, child.Boundary.Height);
                AssertGridAligned(child.Boundary);
            }
        );
        Assert.InRange(parent.ContainerZoom, 0.0001, 1.0);

        var displayedBounds = GetDisplayedBounds(parent);
        var parentArea = parent.Boundary.Width * parent.Boundary.Height;
        var displayedArea = displayedBounds.Width * displayedBounds.Height;
        var occupiedRatio = displayedArea / parentArea;

        Assert.True(occupiedRatio <= 0.255, $"Expected occupied ratio <= 0.255 but was {occupiedRatio:0.###}");
        Assert.True(parent.Children.Select(c => c.Boundary.X).Distinct().Count() > 1);
        Assert.True(parent.Children.Select(c => c.Boundary.Y).Distinct().Count() > 1);
    }

    [Fact]
    public void AdjustChildren_TypeWithMixedChildren_ShouldUseMemberLayoutAndSeparateVisibilityColumns()
    {
        NodeLayout.SetDensity(NodeLayoutDensity.Balanced);

        var root = new ModelNode("", null!) { Type = NodeType.Root };
        var parent = new ModelNode("TypeParent", root) { Type = NodeType.Type, Boundary = new Rect(0, 0, 900, 700) };
        root.AddChild(parent);

        var members = new List<ModelNode>();
        for (var i = 0; i < 16; i++)
        {
            var child = new ModelNode($"Member{i}", parent)
            {
                Type = NodeType.MethodMember,
                IsPrivate = i >= 8,
                Boundary = new Rect(i * 2, i * 3, 60, 60),
            };
            parent.AddChild(child);
            members.Add(child);
        }

        var nestedType1 = new ModelNode("NestedType1", parent)
        {
            Type = NodeType.Type,
            Boundary = new Rect(1, 1, 20, 20),
        };
        var nestedType2 = new ModelNode("NestedType2", parent)
        {
            Type = NodeType.Type,
            Boundary = new Rect(1, 1, 20, 20),
        };
        parent.AddChild(nestedType1);
        parent.AddChild(nestedType2);

        NodeLayout.AdjustChildren(parent);

        Assert.False(parent.IsChildrenLayoutRequired);
        Assert.All(members, child => Assert.Equal(NodeLayout.DefaultSize.Width, child.Boundary.Width));
        Assert.All(members, child => Assert.True(child.Boundary.Height < NodeLayout.DefaultSize.Height));
        Assert.Equal(NodeLayout.DefaultSize.Width, nestedType1.Boundary.Width);
        Assert.Equal(NodeLayout.DefaultSize.Height, nestedType1.Boundary.Height);
        Assert.Equal(NodeLayout.DefaultSize.Width, nestedType2.Boundary.Width);
        Assert.Equal(NodeLayout.DefaultSize.Height, nestedType2.Boundary.Height);
        Assert.All(parent.Children, child => AssertGridAligned(child.Boundary));

        var xCount = members.Select(c => c.Boundary.X).Distinct().Count();
        var yCount = members.Select(c => c.Boundary.Y).Distinct().Count();
        Assert.True(yCount > xCount, $"Expected list-like columns but got xCount={xCount}, yCount={yCount}");

        var yPositions = members.Select(c => c.Boundary.Y).Distinct().OrderBy(y => y).ToList();
        var minStep = yPositions.Zip(yPositions.Skip(1), (y1, y2) => y2 - y1).Min();
        Assert.True(minStep < NodeLayout.DefaultSize.Height);

        var publicMembers = members.Where(m => !(m.IsPrivate ?? false)).ToList();
        var privateMembers = members.Where(m => m.IsPrivate ?? false).ToList();
        var maxPublicX = publicMembers.Max(m => m.Boundary.X + m.Boundary.Width);
        var minPrivateX = privateMembers.Min(m => m.Boundary.X);
        Assert.True(
            minPrivateX > maxPublicX,
            $"Expected separated visibility columns but got {minPrivateX} <= {maxPublicX}"
        );

        var displayedBounds = GetDisplayedBounds(parent);
        var parentArea = parent.Boundary.Width * parent.Boundary.Height;
        var displayedArea = displayedBounds.Width * displayedBounds.Height;
        var occupiedRatio = displayedArea / parentArea;
        Assert.True(occupiedRatio <= 0.255, $"Expected occupied ratio <= 0.255 but was {occupiedRatio:0.###}");
    }

    [Fact]
    public void AdjustChildren_Density_ShouldChangeSpacing()
    {
        var root = new ModelNode("", null!) { Type = NodeType.Root };
        var parent = new ModelNode("Parent", root) { Type = NodeType.Namespace, Boundary = new Rect(0, 0, 1200, 800) };
        root.AddChild(parent);

        for (var i = 0; i < 8; i++)
        {
            var child = new ModelNode($"Child{i}", parent) { Type = NodeType.Type, Boundary = new Rect(0, 0, 40, 40) };
            parent.AddChild(child);
        }

        var oldDensity = NodeLayout.Density;
        try
        {
            NodeLayout.SetDensity(NodeLayoutDensity.Spacious);
            NodeLayout.AdjustChildren(parent);
            var spaciousGap = HorizontalGap(parent.Children);
            var spaciousZoom = parent.ContainerZoom;

            NodeLayout.SetDensity(NodeLayoutDensity.Compact);
            NodeLayout.AdjustChildren(parent);
            var compactGap = HorizontalGap(parent.Children);
            var compactZoom = parent.ContainerZoom;

            Assert.True(
                spaciousGap > compactGap,
                $"Expected spacious gap > compact gap, got {spaciousGap} <= {compactGap}"
            );
            Assert.True(
                compactZoom > spaciousZoom,
                $"Expected compact zoom > spacious zoom, got {compactZoom} <= {spaciousZoom}"
            );
        }
        finally
        {
            NodeLayout.SetDensity(oldDensity);
        }
    }

    [Fact]
    public void AdjustChildren_WhenLayoutIsCustomized_ShouldOnlyPlaceNewChildren()
    {
        NodeLayout.SetDensity(NodeLayoutDensity.Balanced);

        var root = new ModelNode("", null!) { Type = NodeType.Root };
        var parent = new ModelNode("Parent", root)
        {
            Type = NodeType.Namespace,
            Boundary = new Rect(0, 0, 1200, 800),
            ContainerZoom = 0.25,
            ContainerOffset = new Pos(111, 222),
            IsChildrenLayoutCustomized = true,
        };
        root.AddChild(parent);

        var existing1 = new ModelNode("A", parent) { Type = NodeType.Type, Boundary = new Rect(100, 100, 100, 100) };
        var existing2 = new ModelNode("B", parent) { Type = NodeType.Type, Boundary = new Rect(280, 100, 100, 100) };
        parent.AddChild(existing1);
        parent.AddChild(existing2);

        var existing1Before = existing1.Boundary;
        var existing2Before = existing2.Boundary;
        var zoomBefore = parent.ContainerZoom;
        var offsetBefore = parent.ContainerOffset;

        var added = new ModelNode("C", parent) { Type = NodeType.Type, Boundary = Rect.None };
        parent.AddChild(added);

        NodeLayout.AdjustChildren(parent);

        Assert.Equal(existing1Before, existing1.Boundary);
        Assert.Equal(existing2Before, existing2.Boundary);
        Assert.Equal(zoomBefore, parent.ContainerZoom);
        Assert.Equal(offsetBefore, parent.ContainerOffset);

        Assert.NotEqual(Rect.None, added.Boundary);
        Assert.Equal(NodeLayout.DefaultSize.Width, added.Boundary.Width);
        Assert.Equal(NodeLayout.DefaultSize.Height, added.Boundary.Height);
        AssertGridAligned(added.Boundary);
        Assert.False(IsOverlapping(added.Boundary, existing1.Boundary));
        Assert.False(IsOverlapping(added.Boundary, existing2.Boundary));
    }

    [Fact]
    public void GetNextChildRect_RootNode_ShouldPlaceChildrenRowByRow()
    {
        NodeLayout.SetDensity(NodeLayoutDensity.Balanced);

        var root = new ModelNode("", null!)
        {
            Type = NodeType.Root,
            Boundary = new Rect(0, 0, 1000, 1000),
            ContainerZoom = 1,
        };

        var first = NodeLayout.GetNextChildRect(root);
        var firstChild = new ModelNode("Child1", root) { Boundary = first };
        root.AddChild(firstChild);

        var second = NodeLayout.GetNextChildRect(root);

        Assert.Equal(NodeLayout.DefaultSize.Width, first.Width);
        Assert.Equal(NodeLayout.DefaultSize.Height, first.Height);
        AssertGridAligned(first);
        AssertGridAligned(second);
        Assert.Equal(first.Y, second.Y);
        Assert.True(second.X > first.X);
    }

    static Rect GetDisplayedBounds(ModelNode parent)
    {
        var first = parent.Children[0].Boundary;
        var firstX = parent.ContainerOffset.X + first.X * parent.ContainerZoom;
        var firstY = parent.ContainerOffset.Y + first.Y * parent.ContainerZoom;
        var firstX2 = firstX + first.Width * parent.ContainerZoom;
        var firstY2 = firstY + first.Height * parent.ContainerZoom;

        var minX = firstX;
        var minY = firstY;
        var maxX = firstX2;
        var maxY = firstY2;

        foreach (var child in parent.Children.Skip(1))
        {
            var boundary = child.Boundary;
            var x = parent.ContainerOffset.X + boundary.X * parent.ContainerZoom;
            var y = parent.ContainerOffset.Y + boundary.Y * parent.ContainerZoom;
            var x2 = x + boundary.Width * parent.ContainerZoom;
            var y2 = y + boundary.Height * parent.ContainerZoom;

            minX = Math.Min(minX, x);
            minY = Math.Min(minY, y);
            maxX = Math.Max(maxX, x2);
            maxY = Math.Max(maxY, y2);
        }

        return new Rect(minX, minY, maxX - minX, maxY - minY);
    }

    static double HorizontalGap(IReadOnlyList<ModelNode> children)
    {
        var sameRowPairs = children
            .SelectMany(
                c1 => children.Where(c2 => !ReferenceEquals(c1, c2) && Math.Abs(c1.Boundary.Y - c2.Boundary.Y) < 0.001),
                (c1, c2) => (left: c1, right: c2)
            )
            .Where(pair => pair.right.Boundary.X > pair.left.Boundary.X)
            .ToList();

        if (sameRowPairs.Count == 0)
            return 0;

        var pair = sameRowPairs.OrderBy(p => p.right.Boundary.X - p.left.Boundary.X).First();
        return pair.right.Boundary.X - (pair.left.Boundary.X + pair.left.Boundary.Width);
    }

    static bool IsOverlapping(Rect first, Rect second)
    {
        if (first.X + first.Width <= second.X || second.X + second.Width <= first.X)
            return false;
        if (first.Y + first.Height <= second.Y || second.Y + second.Height <= first.Y)
            return false;
        return true;
    }

    static void AssertGridAligned(Rect rect)
    {
        AssertGridAligned(rect.X, nameof(rect.X));
        AssertGridAligned(rect.Y, nameof(rect.Y));
    }

    static void AssertGridAligned(double value, string name)
    {
        var grid = NodeGrid.SnapSize;
        var snapped = Math.Round(value / grid) * grid;
        Assert.True(Math.Abs(value - snapped) < 0.001, $"Expected {name}={value} to align to {grid} grid");
    }
}
