using Dependinator.Core.Parsing;
using Dependinator.UI.Diagrams.Svg;
using Dependinator.UI.Modeling.Models;
using Dependinator.UI.Shared.Types;
using Node = Dependinator.UI.Modeling.Models.Node;

namespace Dependinator.UI.Tests.Diagrams;

// The selection dimming rule: with a node selected only lines touching the node (or its inside)
// stay at full strength; with a line selected only that line; explorer focus lines never dim.
// The same lines, plus focus lines, are the only ones drawn at their link-count width.
public class SvgServiceLineDimTests
{
    static Node CreateRoot() => new("", null!) { Type = NodeType.Root };

    static Node AddNode(Node parent, string name)
    {
        var node = new Node(name, parent) { Type = NodeType.ClassType, Boundary = new Rect(0, 0, 100, 100) };
        parent.AddChild(node);
        return node;
    }

    [Fact]
    public void IsLineDimmed_ShouldKeepLinesTouchingTheSelectedNodeOrItsInside()
    {
        var root = CreateRoot();
        var a = AddNode(root, "A");
        var b = AddNode(root, "B");
        var c = AddNode(root, "C");
        var aChild = AddNode(a, "A.Child");
        var ab = new Line(a, b);
        var bc = new Line(b, c);
        var childC = new Line(aChild, c);

        var selection = new SvgService.RenderSelection(a, null);

        Assert.False(SvgService.IsLineDimmed(ab, selection));
        Assert.True(SvgService.IsLineDimmed(bc, selection));
        Assert.False(SvgService.IsLineDimmed(childC, selection)); // From inside A
    }

    [Fact]
    public void IsLineDimmed_ShouldKeepLinesTouchingAnyNodeOfAGroupSelection()
    {
        var root = CreateRoot();
        var a = AddNode(root, "A");
        var b = AddNode(root, "B");
        var c = AddNode(root, "C");
        var d = AddNode(root, "D");
        var bc = new Line(b, c);
        var cd = new Line(c, d);

        var selection = new SvgService.RenderSelection(a, null, [b]);

        Assert.False(SvgService.IsLineDimmed(bc, selection)); // Touches B, part of the group
        Assert.True(SvgService.IsLineDimmed(cd, selection));
    }

    [Fact]
    public void IsLineDimmed_ShouldKeepOnlyTheSelectedLine()
    {
        var root = CreateRoot();
        var a = AddNode(root, "A");
        var b = AddNode(root, "B");
        var c = AddNode(root, "C");
        var ab = new Line(a, b);
        var bc = new Line(b, c);

        var selection = new SvgService.RenderSelection(null, ab);

        Assert.False(SvgService.IsLineDimmed(ab, selection));
        Assert.True(SvgService.IsLineDimmed(bc, selection));
    }

    [Fact]
    public void IsLineDimmed_ShouldNeverDimFocusLines_AndNothingWithoutSelection()
    {
        var root = CreateRoot();
        var a = AddNode(root, "A");
        var b = AddNode(root, "B");
        var c = AddNode(root, "C");
        var bc = new Line(b, c) { IsFocused = true };

        Assert.False(SvgService.IsLineDimmed(bc, new SvgService.RenderSelection(a, null)));
        Assert.False(SvgService.IsLineDimmed(new Line(b, c), new SvgService.RenderSelection(null, null)));
    }

    [Fact]
    public void IsLineWeighted_ShouldWeightOnlyTheSelectionsLinesAndFocusLines()
    {
        var root = CreateRoot();
        var a = AddNode(root, "A");
        var b = AddNode(root, "B");
        var c = AddNode(root, "C");
        var ab = new Line(a, b);
        var bc = new Line(b, c);
        var focused = new Line(b, c) { IsFocused = true };

        // Nothing selected: every line is plain, except an explorer focus line.
        var none = new SvgService.RenderSelection(null, null);
        Assert.False(SvgService.IsLineWeighted(ab, none));
        Assert.False(SvgService.IsLineWeighted(bc, none));
        Assert.True(SvgService.IsLineWeighted(focused, none));

        // A selected node weights the lines touching it, i.e. the ones dimming keeps bright.
        var nodeSelection = new SvgService.RenderSelection(a, null);
        Assert.True(SvgService.IsLineWeighted(ab, nodeSelection));
        Assert.False(SvgService.IsLineWeighted(bc, nodeSelection));
        Assert.True(SvgService.IsLineWeighted(focused, nodeSelection));

        // A selected line weights only itself.
        var lineSelection = new SvgService.RenderSelection(null, bc);
        Assert.True(SvgService.IsLineWeighted(bc, lineSelection));
        Assert.False(SvgService.IsLineWeighted(ab, lineSelection));
    }
}
