using Dependinator.Core.Parsing;
using Dependinator.UI.Diagrams.Interaction;
using Dependinator.UI.Modeling.Models;
using Dependinator.UI.Shared.Types;
using Node = Dependinator.UI.Modeling.Models.Node;

namespace Dependinator.UI.Tests.Diagrams;

// The rubber band picks nodes fully inside it at the level on screen: a closed container is
// taken whole or not at all, an open one that only partly overlaps is looked into, a node inside
// a taken container is not added separately, and hidden nodes are skipped when not shown.
public class RubberBandSelectionTests
{
    static Node CreateRoot() => new("", null!) { Type = NodeType.Root, ContainerZoom = 1 };

    static Node AddNode(Node parent, string name, Rect boundary, NodeType type = NodeType.ClassType)
    {
        var node = new Node(name, parent)
        {
            Type = type,
            Boundary = boundary,
            ContainerZoom = 1,
            ContainerOffset = new Pos(0, 0),
        };
        parent.AddChild(node);
        return node;
    }

    [Fact]
    public void FindNodes_ShouldTakeNodesFullyInside_AndLookIntoOpenContainersThatOverlap()
    {
        var root = CreateRoot();
        var whole = AddNode(root, "Whole", new Rect(10, 10, 50, 50)); // Fully inside: taken as a unit
        AddNode(whole, "Whole.Child", new Rect(5, 5, 10, 10)); // Inside a taken node: not added
        var open = AddNode(root, "Open", new Rect(100, 0, 200, 200), NodeType.Namespace); // Overlaps, open
        var inner = AddNode(open, "Open.Inner", new Rect(10, 20, 30, 30)); // At canvas (110,20): inside
        AddNode(open, "Open.Outside", new Rect(150, 150, 30, 30)); // At canvas (250,150): outside
        var closed = AddNode(root, "Closed", new Rect(0, 150, 200, 100)); // Overlaps, closed: left alone
        AddNode(closed, "Closed.Inner", new Rect(10, 10, 10, 10));
        AddNode(root, "Far", new Rect(500, 500, 10, 10)); // Not touched

        var band = new Rect(0, 0, 180, 120);
        var found = RubberBandSelection.FindNodes(root, band, isChildrenShown: n => n == open, isVisible: _ => true);

        Assert.Equal([whole, inner], found);
    }

    [Fact]
    public void FindNodes_ShouldLeaveNotesOut()
    {
        var root = CreateRoot();
        var note = AddNode(root, "1", new Rect(10, 10, 20, 20));
        note.IsNote = true;
        note.IsManual = true;
        var node = AddNode(root, "Node", new Rect(40, 10, 20, 20));

        var found = RubberBandSelection.FindNodes(
            root,
            new Rect(0, 0, 100, 100),
            isChildrenShown: _ => false,
            isVisible: _ => true
        );

        Assert.Equal([node], found);
    }

    [Fact]
    public void FindNodes_ShouldSkipHiddenNodes_WhenTheyAreNotShown()
    {
        var root = CreateRoot();
        var shown = AddNode(root, "Shown", new Rect(10, 10, 20, 20));
        var hidden = AddNode(root, "Hidden", new Rect(40, 10, 20, 20));

        var found = RubberBandSelection.FindNodes(
            root,
            new Rect(0, 0, 100, 100),
            isChildrenShown: _ => false,
            isVisible: n => n != hidden
        );

        Assert.Equal([shown], found);
    }
}
