using Dependinator.Core.Parsing;
using Dependinator.UI.Diagrams.Svg;
using Dependinator.UI.Modeling.Models;
using Dependinator.UI.Shared.Types;
using Node = Dependinator.UI.Modeling.Models.Node;

namespace Dependinator.UI.Tests.Diagrams;

// The selection dimming rule: with a node selected only the node's own lines (those leading out of
// it, not those inside it) stay at full strength; with a line selected only that line; explorer
// focus lines never dim.
// The same lines, plus focus lines, are the only ones drawn at their link-count width.
public class LineEmphasisDimTests
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

        var selection = new RenderSelection(a, null);

        Assert.False(LineEmphasis.IsLineDimmed(ab, selection));
        Assert.True(LineEmphasis.IsLineDimmed(bc, selection));
        Assert.False(LineEmphasis.IsLineDimmed(childC, selection)); // From inside A
    }

    [Fact]
    public void IsLineDimmed_ShouldKeepTheLinesDrawnForTheSelectedLine()
    {
        var root = CreateRoot();
        var a = AddNode(root, "A");
        var b = AddNode(root, "B");
        var c = AddNode(root, "C");
        var stub = new Line(root, b); // From the container's edge to its child
        var itsLine = new Line(c, b) { IsRevealed = true }; // Drawn for the selected stub
        var other = new Line(a, c);

        var selection = new RenderSelection(null, stub);

        Assert.False(LineEmphasis.IsLineDimmed(stub, selection));
        Assert.False(LineEmphasis.IsLineDimmed(itsLine, selection));
        Assert.True(LineEmphasis.IsLineDimmed(other, selection));
        Assert.True(LineEmphasis.IsLineLit(itsLine, selection));

        // Only a stub has lines of its own: beside another selected line (one of a node's lines,
        // clicked) a line that shares its ends is dimmed like the rest.
        Assert.True(LineEmphasis.IsLineDimmed(itsLine, new RenderSelection(null, other)));
    }

    [Fact]
    public void IsLineDimmed_ShouldDimLinesBetweenTheSelectedNodesChildren()
    {
        var root = CreateRoot();
        var a = AddNode(root, "A");
        var b = AddNode(root, "B");
        var child1 = AddNode(a, "A.Child1");
        var child2 = AddNode(a, "A.Child2");
        var grandChild = AddNode(child2, "A.Child2.GrandChild");
        var between = new Line(child1, child2);
        var deep = new Line(child1, grandChild);
        var toEdge = new Line(child1, a); // A child's segment out to A's edge
        var intoA = new Line(b, grandChild); // From outside into A

        var selection = new RenderSelection(a, null);

        Assert.True(LineEmphasis.IsLineDimmed(between, selection));
        Assert.True(LineEmphasis.IsLineDimmed(deep, selection));
        Assert.True(LineEmphasis.IsLineDimmed(toEdge, selection));
        Assert.False(LineEmphasis.IsLineDimmed(intoA, selection));

        // Selecting a child lights the child's lines again.
        var childSelection = new RenderSelection(child1, null);
        Assert.False(LineEmphasis.IsLineDimmed(between, childSelection));
        Assert.False(LineEmphasis.IsLineDimmed(deep, childSelection));
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

        var selection = new RenderSelection(a, null, [b]);

        Assert.False(LineEmphasis.IsLineDimmed(bc, selection)); // Touches B, part of the group
        Assert.True(LineEmphasis.IsLineDimmed(cd, selection));
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

        var selection = new RenderSelection(null, ab);

        Assert.False(LineEmphasis.IsLineDimmed(ab, selection));
        Assert.True(LineEmphasis.IsLineDimmed(bc, selection));
    }

    [Fact]
    public void IsLineDimmed_ShouldNeverDimFocusLines_AndNothingWithoutSelection()
    {
        var root = CreateRoot();
        var a = AddNode(root, "A");
        var b = AddNode(root, "B");
        var c = AddNode(root, "C");
        var bc = new Line(b, c) { IsFocused = true };

        Assert.False(LineEmphasis.IsLineDimmed(bc, new RenderSelection(a, null)));
        Assert.False(LineEmphasis.IsLineDimmed(new Line(b, c), new RenderSelection(null, null)));
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
        var none = new RenderSelection(null, null);
        Assert.False(LineEmphasis.IsLineWeighted(ab, none, true));
        Assert.False(LineEmphasis.IsLineWeighted(bc, none, true));
        Assert.True(LineEmphasis.IsLineWeighted(focused, none, true));

        // A selected node weights the lines touching it, i.e. the ones dimming keeps bright.
        var nodeSelection = new RenderSelection(a, null);
        Assert.True(LineEmphasis.IsLineWeighted(ab, nodeSelection, true));
        Assert.False(LineEmphasis.IsLineWeighted(bc, nodeSelection, true));
        Assert.True(LineEmphasis.IsLineWeighted(focused, nodeSelection, true));

        // A selected line weights only itself.
        var lineSelection = new RenderSelection(null, bc);
        Assert.True(LineEmphasis.IsLineWeighted(bc, lineSelection, true));
        Assert.False(LineEmphasis.IsLineWeighted(ab, lineSelection, true));
    }

    [Fact]
    public void IsLineWeighted_ShouldWeightNothing_WhenLinkCountWidthsAreOff()
    {
        var root = CreateRoot();
        var a = AddNode(root, "A");
        var b = AddNode(root, "B");
        var ab = new Line(a, b);
        var focused = new Line(a, b) { IsFocused = true };

        var nodeSelection = new RenderSelection(a, null);
        Assert.False(LineEmphasis.IsLineWeighted(ab, nodeSelection, false));
        Assert.False(LineEmphasis.IsLineWeighted(focused, nodeSelection, false));
        Assert.False(LineEmphasis.IsLineWeighted(ab, new RenderSelection(null, ab), false));
    }
}
