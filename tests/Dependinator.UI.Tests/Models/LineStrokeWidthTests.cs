using Dependinator.Core.Parsing;
using Dependinator.UI.Modeling.Models;
using Dependinator.UI.Shared.Types;
using Link = Dependinator.UI.Modeling.Models.Link;
using Node = Dependinator.UI.Modeling.Models.Node;

namespace Dependinator.UI.Tests.Models;

// A line's link-count width is a log scale (half a pixel per doubling, capped at 3), so the
// few lines of a selected node tell their counts apart; direct and hidden lines have fixed widths.
public class LineStrokeWidthTests
{
    static Node CreateRoot() => new("", null!) { Type = NodeType.Root };

    static Node AddNode(Node parent, string name)
    {
        var node = new Node(name, parent) { Type = NodeType.ClassType, Boundary = new Rect(0, 0, 100, 100) };
        parent.AddChild(node);
        return node;
    }

    // A line between A and B carrying one link per member of A (distinct link ids).
    static Line CreateLine(int linkCount, bool isDirect = false)
    {
        var root = CreateRoot();
        var a = AddNode(root, "A");
        var b = AddNode(root, "B");
        var line = new Line(a, b, isDirect);
        for (var i = 0; i < linkCount; i++)
        {
            line.Add(new Link(AddNode(a, $"A.M{i}"), b));
        }
        return line;
    }

    [Theory]
    [InlineData(0, 1)]
    [InlineData(1, 1)]
    [InlineData(2, 1.5)]
    [InlineData(4, 2)]
    [InlineData(7, 2.4)]
    [InlineData(8, 2.5)]
    [InlineData(16, 3)]
    [InlineData(64, 3)]
    public void WeightedStrokeWidth_ShouldGrowPerDoublingAndCap(int linkCount, double expectedWidth)
    {
        var line = CreateLine(linkCount);

        Assert.Equal(expectedWidth, line.WeightedStrokeWidth, 1);
    }

    [Fact]
    public void WeightedStrokeWidth_ShouldBeFixedForDirectAndHiddenLines()
    {
        Assert.Equal(2, CreateLine(16, isDirect: true).WeightedStrokeWidth);

        var hidden = CreateLine(16);
        hidden.IsHidden = true;
        Assert.Equal(1, hidden.WeightedStrokeWidth);
    }

    [Fact]
    public void WeightedStrokeWidth_ShouldFollowLinkRemoval()
    {
        var line = CreateLine(4);
        Assert.Equal(2, line.WeightedStrokeWidth);

        foreach (var link in line.Links.Skip(1).ToList())
        {
            line.Remove(link);
        }

        Assert.Equal(1, line.WeightedStrokeWidth);
    }
}
