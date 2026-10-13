using Dependinator.Core.Parsing;
using Dependinator.UI.Diagrams.Interaction;
using Dependinator.UI.Shared.Types;
using Node = Dependinator.UI.Modeling.Models.Node;

namespace Dependinator.UI.Tests.Diagrams;

// Dragging a zone carries the sibling nodes drawn fully inside it; a node on the edge or outside
// stays, and so does the zone's parent and anything in another container.
public class ZoneContentsTests
{
    static Node AddNode(Node parent, string name, Rect boundary, bool isZone = false)
    {
        var node = new Node(name, parent)
        {
            Type = NodeType.ClassType,
            Boundary = boundary,
            IsZone = isZone,
            IsManual = isZone,
        };
        parent.AddChild(node);
        return node;
    }

    [Fact]
    public void NodesInside_ShouldReturnTheSiblingsFullyInsideTheZone()
    {
        var root = new Node("", null!) { Type = NodeType.Root };
        var container = AddNode(root, "C", new Rect(0, 0, 1000, 1000));
        var zone = AddNode(container, "C.Edge", new Rect(100, 100, 400, 300), isZone: true);
        var inside = AddNode(container, "C.A", new Rect(120, 120, 100, 100));
        var nested = AddNode(container, "C.Inner", new Rect(300, 150, 150, 100), isZone: true);
        var onEdge = AddNode(container, "C.B", new Rect(450, 150, 100, 100));
        var outside = AddNode(container, "C.D", new Rect(600, 600, 100, 100));
        AddNode(inside, "C.A.x", new Rect(0, 0, 10, 10));

        var found = ZoneContents.NodesInside(zone);

        Assert.Equal([inside, nested], found);
        Assert.DoesNotContain(onEdge, found);
        Assert.DoesNotContain(outside, found);
        Assert.DoesNotContain(zone, found);
    }

    [Fact]
    public void NodesInside_ShouldBeEmpty_ForAnOrphanZone()
    {
        var zone = new Node("Edge", null!) { IsZone = true, Boundary = new Rect(0, 0, 100, 100) };

        Assert.Empty(ZoneContents.NodesInside(zone));
    }
}
