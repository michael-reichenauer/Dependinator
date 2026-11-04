using Dependinator.Core.Parsing;
using Dependinator.UI.Modeling;
using Dependinator.UI.Shared.Types;
using ModelNode = Dependinator.UI.Modeling.Models.Node;

namespace Dependinator.UI.Tests.Models;

// The member card (MemberCard): a container's member rows grouped into titled sections and flowed
// into columns, with headings and the card's bounds derived from the row positions.
public class MemberCardTests
{
    [Fact]
    public void Sections_ShouldFollowTheClassBoxOrder_WithPublicMembersFirst()
    {
        var type = Type();
        var privateMethod = Member(type, "b", NodeType.MethodMember, isPrivate: true);
        var publicMethod = Member(type, "c", NodeType.MethodMember);
        var field = Member(type, "a", NodeType.FieldMember, isPrivate: true);
        var property = Member(type, "d", NodeType.PropertyMember);
        var constructor = Member(type, "e", NodeType.ConstructorMember);

        var sections = MemberCard.Sections(type.Children, ByName);

        Assert.Equal(["Fields", "Constructors", "Properties", "Methods"], sections.Select(s => s.Title));
        Assert.Equal([field], sections[0].Rows);
        Assert.Equal([constructor], sections[1].Rows);
        Assert.Equal([property], sections[2].Rows);
        Assert.Equal([publicMethod, privateMethod], sections[3].Rows);
    }

    [Fact]
    public void Arrange_ShouldPutEachSectionsHeadingAboveItsFirstRow()
    {
        var type = Type();
        var field = Member(type, "field", NodeType.FieldMember);
        var method1 = Member(type, "method1", NodeType.MethodMember);
        var method2 = Member(type, "method2", NodeType.MethodMember);

        // A tall target: one column
        var card = MemberCard.Arrange(MemberCard.Sections(type.Children, ByName), 40, 40, 0.2);

        Assert.Equal(
            new Rect(40, 40 + MemberCard.RowPitch, field.Boundary.Width, MemberCard.RowHeight),
            field.Boundary
        );
        Assert.Equal(40 + 3 * MemberCard.RowPitch, method1.Boundary.Y); // Below the Methods heading
        Assert.Equal(40 + 4 * MemberCard.RowPitch, method2.Boundary.Y);

        var headings = MemberCard.Headings(type).OrderBy(h => h.Y).ToList();
        Assert.Equal(["Fields", "Methods"], headings.Select(h => h.Title));
        Assert.Equal(40, headings[0].Y);
        Assert.True(headings[0].IsColumnTop);
        Assert.Equal(40 + 2 * MemberCard.RowPitch, headings[1].Y);
        Assert.False(headings[1].IsColumnTop); // Below the fields: drawn with a divider

        Assert.Equal(card, MemberCard.Bounds(type));
        Assert.Equal(40 - MemberCard.Padding, card.X);
        Assert.Equal(40 - MemberCard.Padding, card.Y);
    }

    [Fact]
    public void Arrange_ShouldFlowIntoMoreColumns_ForAWiderTarget()
    {
        var type = Type();
        for (var i = 0; i < 30; i++)
            Member(type, $"method{i:00}", NodeType.MethodMember);
        var sections = MemberCard.Sections(type.Children, ByName);

        MemberCard.Arrange(sections, 0, 0, 0.2);
        var tallColumns = type.Children.Select(c => c.Boundary.X).Distinct().Count();
        var tall = MemberCard.Bounds(type);

        MemberCard.Arrange(sections, 0, 0, 3);
        var wideColumns = type.Children.Select(c => c.Boundary.X).Distinct().Count();
        var wide = MemberCard.Bounds(type);

        Assert.Equal(1, tallColumns);
        Assert.True(wideColumns > 2, $"Expected several columns but got {wideColumns}");
        Assert.True(wide.Width / wide.Height > tall.Width / tall.Height);
        // The rows keep their order down each column, then on in the next column
        var order = type.Children.OrderBy(c => c.Boundary.X).ThenBy(c => c.Boundary.Y).Select(c => c.Name);
        Assert.Equal(type.Children.Select(c => c.Name).Order(), order);
    }

    [Fact]
    public void Arrange_ShouldNeverLeaveAHeadingAtTheBottomOfAColumn()
    {
        var type = Type();
        for (var i = 0; i < 5; i++)
            Member(type, $"field{i}", NodeType.FieldMember);
        for (var i = 0; i < 5; i++)
            Member(type, $"method{i}", NodeType.MethodMember);

        MemberCard.Arrange(MemberCard.Sections(type.Children, ByName), 0, 0, 1.5);

        foreach (var heading in MemberCard.Headings(type))
        {
            var rowBelow = type.Children.Any(c =>
                c.Boundary.X == heading.X && c.Boundary.Y == heading.Y + MemberCard.HeadingHeight
            );
            Assert.True(rowBelow, $"Heading {heading.Title} has no row below it");
        }
    }

    [Fact]
    public void NaturalAspect_ShouldBeCloserToASquare_ThanASingleColumn()
    {
        var type = Type();
        for (var i = 0; i < 40; i++)
            Member(type, $"method{i:00}", NodeType.MethodMember);
        var sections = MemberCard.Sections(type.Children, ByName);

        var aspect = MemberCard.NaturalAspect(sections);
        MemberCard.Arrange(sections, 0, 0, 0.01);
        var column = MemberCard.Bounds(type);

        Assert.True(Math.Abs(Math.Log(aspect)) < Math.Abs(Math.Log(column.Width / column.Height)));
    }

    [Fact]
    public void Arrange_ShouldMakeColumnsWideEnoughForTheLongestName()
    {
        var type = Type();
        var shortName = Member(type, "a", NodeType.MethodMember);
        Member(type, "AVeryLongMethodNameThatNeedsRoom", NodeType.MethodMember);

        MemberCard.Arrange(MemberCard.Sections(type.Children, ByName), 0, 0, 1);

        Assert.True(shortName.Boundary.Width > 100, $"Expected a wide column but got {shortName.Boundary.Width}");
        Assert.Equal(0, shortName.Boundary.Width % 20);
    }

    [Fact]
    public void Headings_ShouldLeaveOutSectionsWhoseRowsAreNotShown()
    {
        var type = Type();
        var field = Member(type, "field", NodeType.FieldMember);
        Member(type, "method", NodeType.MethodMember);
        MemberCard.Arrange(MemberCard.Sections(type.Children, ByName), 0, 0, 0.2);

        var headings = MemberCard.Headings(type, row => row.Type != NodeType.FieldMember);

        Assert.Equal(["Methods"], headings.Select(h => h.Title));
        Assert.True(MemberCard.Bounds(type, row => row.Type != NodeType.FieldMember).Y > field.Boundary.Y);
    }

    static int ByName(ModelNode a, ModelNode b) => string.CompareOrdinal(a.Name, b.Name);

    static ModelNode Type()
    {
        var root = new ModelNode("", null!) { Type = NodeType.Root };
        var type = new ModelNode("Type", root) { Type = NodeType.ClassType };
        root.AddChild(type);
        return type;
    }

    static ModelNode Member(ModelNode type, string name, NodeType memberType, bool isPrivate = false)
    {
        var member = new ModelNode($"Type.{name}", type) { Type = memberType, IsPrivate = isPrivate };
        type.AddChild(member);
        return member;
    }
}
