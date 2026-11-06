using Dependinator.Core.Parsing;
using Dependinator.UI.Modeling;
using Dependinator.UI.Shared.Types;
using ModelNode = Dependinator.UI.Modeling.Models.Node;

namespace Dependinator.UI.Tests.Models;

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
    public void AdjustChildren_TypeWithMixedChildren_ShouldPutMembersUnderTheHeaderAndNestedTypesBelow()
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
        // The rows share one column width and the card's row height, the nested types keep their size
        var columnWidth = members[0].Boundary.Width;
        Assert.InRange(columnWidth, 100, 220);
        Assert.All(members, child => Assert.Equal(columnWidth, child.Boundary.Width));
        Assert.All(members, child => Assert.Equal(MemberCard.RowHeight, child.Boundary.Height));
        Assert.Equal(NodeLayout.DefaultSize.Width, nestedType1.Boundary.Width);
        Assert.Equal(NodeLayout.DefaultSize.Height, nestedType1.Boundary.Height);
        Assert.Equal(NodeLayout.DefaultSize.Width, nestedType2.Boundary.Width);
        AssertGridAligned(nestedType1.Boundary);
        AssertGridAligned(nestedType2.Boundary);

        // One row per step
        var yPositions = members.Select(c => c.Boundary.Y).Distinct().OrderBy(y => y).ToList();
        Assert.Equal(MemberCard.RowPitch, yPositions.Zip(yPositions.Skip(1), (y1, y2) => y2 - y1).Min());

        // Public members come first in card order (columns left to right, rows top to bottom)
        var cardOrder = members.OrderBy(m => m.Boundary.X).ThenBy(m => m.Boundary.Y).ToList();
        Assert.Equal(
            cardOrder.Select(m => m.IsPrivate ?? false).OrderBy(isPrivate => isPrivate),
            cardOrder.Select(m => m.IsPrivate ?? false)
        );

        // The type is a UML card: its children start at its top left, the rows under the header,
        // the nested types below the rows
        var card = MemberCard.Bounds(parent);
        Assert.Equal(0, card.X);
        Assert.Equal(MemberCard.CardRowsTop, members.Min(m => m.Boundary.Y) - MemberCard.HeadingHeight);
        Assert.Equal(new Pos(0, 0), parent.ContainerOffset);
        Assert.True(nestedType1.Boundary.Y >= card.Y + card.Height);
        Assert.True(nestedType2.Boundary.Y >= card.Y + card.Height);
        var displayedBounds = GetDisplayedBounds(parent);
        Assert.True(displayedBounds.X + displayedBounds.Width <= parent.Boundary.Width + 0.001);
        Assert.True(displayedBounds.Y + displayedBounds.Height <= parent.Boundary.Height + 0.001);
    }

    [Fact]
    public void CardTransform_ShouldKeepTheRowsUnderTheHeader_ForAResizedBox()
    {
        var (parent, _, _) = CardType();
        NodeLayout.AdjustChildren(parent);
        Assert.Equal((parent.ContainerZoom, parent.ContainerOffset), NodeLayout.CardTransform(parent, parent.Boundary));

        // Resized from the top left handle: the children still start at the box's top left
        var (zoom, offset) = NodeLayout.CardTransform(parent, new Rect(-50, -40, 300, 220));
        Assert.Equal(new Pos(0, 0), offset);
        Assert.True(zoom < parent.ContainerZoom, $"Expected a smaller zoom for a smaller box but got {zoom}");
    }

    [Fact]
    public void AdjustChildren_CustomizedCard_ShouldMovePlacedChildrenBelowTheRows()
    {
        var (parent, members, nested) = CardType();
        NodeLayout.AdjustChildren(parent);
        parent.IsChildrenLayoutCustomized = true;
        nested.Boundary = nested.Boundary with { Y = 0 }; // Placed where the rows have grown to

        NodeLayout.AdjustChildren(parent);

        var card = MemberCard.Bounds(parent);
        Assert.True(nested.Boundary.Y >= card.Y + card.Height, $"Expected below {card} but got {nested.Boundary}");
        Assert.Equal(MemberCard.CardRowsTop, members.Min(m => m.Boundary.Y) - MemberCard.HeadingHeight);
    }

    static (ModelNode Parent, List<ModelNode> Members, ModelNode Nested) CardType()
    {
        NodeLayout.SetDensity(NodeLayoutDensity.Balanced);
        var root = new ModelNode("", null!) { Type = NodeType.Root };
        var parent = new ModelNode("CardType", root) { Type = NodeType.Type, Boundary = new Rect(0, 0, 400, 300) };
        root.AddChild(parent);
        var members = new List<ModelNode>();
        for (var i = 0; i < 6; i++)
        {
            var member = new ModelNode($"CardType.Member{i}", parent) { Type = NodeType.MethodMember };
            parent.AddChild(member);
            members.Add(member);
        }
        var nested = new ModelNode("CardType.Nested", parent) { Type = NodeType.Type };
        parent.AddChild(nested);
        return (parent, members, nested);
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
    public void AdjustChildren_TypeMembers_ShouldGroupBySectionThenImportance()
    {
        NodeLayout.SetDensity(NodeLayoutDensity.Balanced);

        var root = new ModelNode("", null!) { Type = NodeType.Root };
        var parent = new ModelNode("TypeParent", root)
        {
            Type = NodeType.ClassType,
            Boundary = new Rect(0, 0, 100, 400),
        };
        root.AddChild(parent);

        var method = new ModelNode("TypeParent.Method()", parent) { Type = NodeType.MethodMember, IsPrivate = false };
        var other = new ModelNode("TypeParent.Aaa()", parent) { Type = NodeType.MethodMember, IsPrivate = false };
        var constructor = new ModelNode("TypeParent.Ctor()", parent)
        {
            Type = NodeType.ConstructorMember,
            IsPrivate = false,
        };
        var property = new ModelNode("TypeParent.Property", parent)
        {
            Type = NodeType.PropertyMember,
            IsPrivate = false,
        };
        parent.AddChild(method);
        parent.AddChild(other);
        parent.AddChild(constructor);
        parent.AddChild(property);

        // Two references make Method the most important method, ahead of Aaa
        var user1 = new ModelNode("User1", root) { Type = NodeType.Type };
        var user2 = new ModelNode("User2", root) { Type = NodeType.Type };
        root.AddChild(user1);
        root.AddChild(user2);
        foreach (var user in new[] { user1, user2 })
        {
            var link = new Dependinator.UI.Modeling.Models.Link(user, method);
            user.AddSourceLink(link);
            method.AddTargetLink(link);
        }

        NodeLayout.AdjustChildren(parent);

        // A tall type: one column, sections in the class-box order, then by importance
        Assert.Single(parent.Children.Select(c => c.Boundary.X).Distinct());
        Assert.True(constructor.Boundary.Y < property.Boundary.Y, "Expected constructors before properties");
        Assert.True(property.Boundary.Y < method.Boundary.Y, "Expected properties before methods");
        Assert.True(method.Boundary.Y < other.Boundary.Y, "Expected the most referenced method first");
        Assert.Equal(
            ["Constructors", "Properties", "Methods"],
            MemberCard.Headings(parent).OrderBy(h => h.Y).Select(h => h.Title)
        );
    }

    [Fact]
    public void AdjustChildren_ChildReferencingParent_ShouldBePlacedFirstRegardlessOfInsertionOrder()
    {
        NodeLayout.SetDensity(NodeLayoutDensity.Balanced);

        var root = new ModelNode("", null!) { Type = NodeType.Root };
        var parent = new ModelNode("Parent", root) { Type = NodeType.Namespace, Boundary = new Rect(0, 0, 1200, 800) };
        root.AddChild(parent);

        // Insert the unrelated child first, so only a symmetric comparison moves the
        // parent-referencing child to the front.
        var unrelated = new ModelNode("Parent.B", parent) { Type = NodeType.Type };
        var referencing = new ModelNode("Parent.A", parent) { Type = NodeType.Type };
        parent.AddChild(unrelated);
        parent.AddChild(referencing);

        // An empty line (no links) is ignored by the layered layout, so the sorted grid
        // fallback that orders children by their line relations is used.
        var line = new Dependinator.UI.Modeling.Models.Line(referencing, parent);
        referencing.SourceLines.Add(line);
        parent.TargetLines.Add(line);

        NodeLayout.AdjustChildren(parent);

        var first = referencing.Boundary;
        var second = unrelated.Boundary;
        Assert.True(
            first.Y < second.Y || (first.Y == second.Y && first.X < second.X),
            $"Expected parent-referencing child first but got {first} vs {second}"
        );
    }

    [Fact]
    public void PackShelves_ShouldNotOverlapAndCenterEachRowVertically()
    {
        List<Size> sizes = [new(180, 180), new(140, 140), new(100, 100), new(60, 60), new(60, 60)];

        var (positions, blockSize) = NodeLayout.PackShelves(sizes, 1.0, new LayoutMetrics(100, 100, 70, 70, 1.0));

        var rects = sizes
            .Select((size, index) => new Rect(positions[index].X, positions[index].Y, size.Width, size.Height))
            .ToList();
        Assert.All(rects, AssertGridAligned);
        for (var first = 0; first < rects.Count; first++)
        {
            Assert.InRange(rects[first].X + rects[first].Width, 0, blockSize.Width);
            Assert.InRange(rects[first].Y + rects[first].Height, 0, blockSize.Height);
            for (var second = first + 1; second < rects.Count; second++)
            {
                Assert.False(IsOverlapping(rects[first], rects[second]));
                var isSameRow =
                    rects[first].Y < rects[second].Y + rects[second].Height
                    && rects[second].Y < rects[first].Y + rects[first].Height;
                if (isSameRow)
                    Assert.Equal(rects[first].Y + rects[first].Height / 2, rects[second].Y + rects[second].Height / 2);
            }
        }
    }

    [Fact]
    public void PackShelves_EqualSizes_ShouldFormTheGridClosestToTheTargetAspect()
    {
        var sizes = Enumerable.Repeat(new Size(100, 100), 12).ToList();

        var (positions, _) = NodeLayout.PackShelves(sizes, 4.0 / 3, new LayoutMetrics(100, 100, 70, 70, 1.0));

        Assert.Equal(4, positions.Select(position => position.X).Distinct().Count());
        Assert.Equal(3, positions.Select(position => position.Y).Distinct().Count());
    }

    [Fact]
    public void AdjustChildren_NoRelations_ShouldKeepTheOrderOfEquallyLargeChildren()
    {
        NodeLayout.SetDensity(NodeLayoutDensity.Balanced);
        var root = new ModelNode("", null!) { Type = NodeType.Root };
        var parent = new ModelNode("Parent", root) { Type = NodeType.Namespace, Boundary = new Rect(0, 0, 1200, 800) };
        root.AddChild(parent);
        var names = new[] { "Zeta", "Alpha", "Mid" };
        foreach (var name in names)
            parent.AddChild(new ModelNode($"Parent.{name}", parent) { Type = NodeType.ClassType });

        NodeLayout.AdjustChildren(parent);

        var ordered = parent
            .Children.OrderBy(child => child.Boundary.Y)
            .ThenBy(child => child.Boundary.X)
            .Select(child => child.Name)
            .ToList();
        Assert.Equal(names.Select(name => $"Parent.{name}"), ordered);
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

    // C# types hold member rows; so do Bicep files and modules (params, vars, outputs next to
    // resources), so the row layout keys on the children, not on the parent being a type.
    // NodeType is internal, so the theory takes the enum member name.
    [Theory]
    [InlineData("ClassType")]
    [InlineData("Solution")]
    [InlineData("Module")]
    public void AdjustChildren_ShouldLayOutMembersAsRows_WhateverTheParentType(string parentType)
    {
        NodeLayout.SetDensity(NodeLayoutDensity.Balanced);

        var root = new ModelNode("", null!) { Type = NodeType.Root };
        var parent = new ModelNode("Parent", root) { Type = Enum.Parse<NodeType>(parentType) };
        root.AddChild(parent);
        var first = new ModelNode("Parent.first", parent) { Type = NodeType.ParameterMember };
        var second = new ModelNode("Parent.second", parent) { Type = NodeType.OutputMember };
        var resource = new ModelNode("Parent.resource", parent) { Type = NodeType.Resource };
        parent.AddChild(first);
        parent.AddChild(second);
        parent.AddChild(resource);

        NodeLayout.AdjustChildren(parent);

        Assert.Equal(MemberCard.RowHeight, first.Boundary.Height);
        Assert.Equal(MemberCard.RowHeight, second.Boundary.Height);
        Assert.Equal(NodeLayout.DefaultSize.Height, resource.Boundary.Height);
        Assert.Equal(first.Boundary.X, second.Boundary.X);
        Assert.True(second.Boundary.Y > first.Boundary.Y);
        Assert.True(resource.Boundary.X > first.Boundary.X);
    }

    [Fact]
    public void AdjustChildren_ShouldLayOutAuxiliaryResourcesAsRows_UnderAResource()
    {
        // A storage account with a role assignment, a diagnostic setting and a child resource:
        // the auxiliary resources are rows, the child resource a box next to them.
        NodeLayout.SetDensity(NodeLayoutDensity.Balanced);

        var root = new ModelNode("", null!) { Type = NodeType.Root };
        var account = new ModelNode("account", root) { Type = NodeType.Resource };
        root.AddChild(account);
        var role = new ModelNode("account.role", account) { Type = NodeType.ResourceMember };
        var diagnostics = new ModelNode("account.diagnostics", account) { Type = NodeType.ResourceMember };
        var blobs = new ModelNode("account.blobs", account) { Type = NodeType.Resource };
        account.AddChild(role);
        account.AddChild(diagnostics);
        account.AddChild(blobs);

        NodeLayout.AdjustChildren(account);

        Assert.Equal(MemberCard.RowHeight, role.Boundary.Height);
        Assert.Equal(MemberCard.RowHeight, diagnostics.Boundary.Height);
        Assert.Equal(NodeLayout.DefaultSize.Height, blobs.Boundary.Height);
        Assert.Equal(role.Boundary.X, diagnostics.Boundary.X);
        Assert.True(blobs.Boundary.X > role.Boundary.X);
    }

    [Fact]
    public void AdjustChildren_ShouldUseGridLayout_WhenNoChildIsAMember()
    {
        NodeLayout.SetDensity(NodeLayoutDensity.Balanced);

        var root = new ModelNode("", null!) { Type = NodeType.Root };
        var parent = new ModelNode("Parent", root) { Type = NodeType.Solution };
        root.AddChild(parent);
        var first = new ModelNode("Parent.first", parent) { Type = NodeType.Resource };
        var second = new ModelNode("Parent.second", parent) { Type = NodeType.Module };
        parent.AddChild(first);
        parent.AddChild(second);

        NodeLayout.AdjustChildren(parent);

        Assert.Equal(NodeLayout.DefaultSize.Height, first.Boundary.Height);
        Assert.Equal(NodeLayout.DefaultSize.Height, second.Boundary.Height);
    }

    [Fact]
    public void AdjustChildren_ShouldKeepZonesAndNotesWhereTheyAre_WhenForced()
    {
        // "Reset Children Layout" rearranges the parsed nodes but never the user's annotations.
        NodeLayout.SetDensity(NodeLayoutDensity.Balanced);

        var root = new ModelNode("", null!) { Type = NodeType.Root };
        var parent = new ModelNode("Parent", root) { Type = NodeType.Namespace };
        root.AddChild(parent);
        var zoneRect = new Dependinator.UI.Shared.Types.Rect(500, 500, 320, 200);
        var noteRect = new Dependinator.UI.Shared.Types.Rect(700, 20, 40, 40);
        var zone = new ModelNode("Parent.Edge", parent)
        {
            IsManual = true,
            IsZone = true,
            Boundary = zoneRect,
        };
        var note = new ModelNode("Parent.1", parent)
        {
            IsManual = true,
            IsNote = true,
            Boundary = noteRect,
        };
        var a = new ModelNode("Parent.A", parent)
        {
            Type = NodeType.ClassType,
            Boundary = new Dependinator.UI.Shared.Types.Rect(900, 900, 100, 100),
        };
        var b = new ModelNode("Parent.B", parent)
        {
            Type = NodeType.ClassType,
            Boundary = new Dependinator.UI.Shared.Types.Rect(950, 950, 100, 100),
        };
        parent.AddChild(zone);
        parent.AddChild(note);
        parent.AddChild(a);
        parent.AddChild(b);
        parent.IsChildrenLayoutCustomized = true;

        NodeLayout.AdjustChildren(parent, forceAllChildren: true);

        Assert.Equal(zoneRect, zone.Boundary);
        Assert.Equal(noteRect, note.Boundary);
        Assert.NotEqual(new Dependinator.UI.Shared.Types.Rect(900, 900, 100, 100), a.Boundary);
        Assert.NotEqual(new Dependinator.UI.Shared.Types.Rect(950, 950, 100, 100), b.Boundary);
        Assert.NotEqual(a.Boundary, b.Boundary);
    }
}
