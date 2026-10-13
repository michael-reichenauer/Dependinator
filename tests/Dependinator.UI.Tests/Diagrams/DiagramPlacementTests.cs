using Dependinator.Core.Parsing;
using Dependinator.UI.Diagrams.Interaction;
using Dependinator.UI.Modeling;
using Dependinator.UI.Modeling.Models;
using Dependinator.UI.Shared;
using Dependinator.UI.Shared.Types;
using Node = Dependinator.UI.Modeling.Models.Node;

namespace Dependinator.UI.Tests.Diagrams;

// Where a new item lands when the user clicks on something: a zone is background, so a click on
// it adds next to it, into the zone's parent.
public class DiagramPlacementTests
{
    static Node AddNode(IModel model, string name, Node parent, Rect boundary, NodeType type = NodeType.ClassType)
    {
        var node = new Node(name, parent)
        {
            Type = type,
            Boundary = boundary,
            ContainerZoom = 1,
        };
        parent.AddChild(node);
        model.TryAddNode(node);
        return node;
    }

    [Fact]
    public void ResolveContainer_ShouldReturnTheZonesParent_ForAClickOnAZone()
    {
        using var model = new ModelMgr(new StateMgr()).UseModel();
        var container = AddNode(model, "C", model.Root, new Rect(0, 0, 1000, 1000), NodeType.Namespace);
        var zone = AddNode(model, "C.Edge", container, new Rect(0, 0, 900, 900));
        zone.IsZone = true;
        zone.IsManual = true;

        // Whatever the zoom: a zone is never a container of its own.
        foreach (var zoom in new[] { 0.01, 1.0, 100.0 })
        {
            model.Zoom = zoom;
            Assert.Same(container, DiagramPlacement.ResolveContainer(model, PointerId.FromNode(zone.Id)));
        }
    }

    [Fact]
    public void ResolveContainer_ShouldReturnTheRoot_ForTheCanvas()
    {
        using var model = new ModelMgr(new StateMgr()).UseModel();

        Assert.Same(model.Root, DiagramPlacement.ResolveContainer(model, PointerId.Parse("svgcanvas")));
    }

    [Fact]
    public void UniqueShortName_ShouldAppendANumber_WhenTheNameIsTaken()
    {
        using var model = new ModelMgr(new StateMgr()).UseModel();
        var container = AddNode(model, "C", model.Root, new Rect(0, 0, 100, 100));
        AddNode(model, "C.Zone", container, new Rect(0, 0, 10, 10));
        AddNode(model, "C.Zone 2", container, new Rect(0, 0, 10, 10));

        Assert.Equal("Zone 3", DiagramPlacement.UniqueShortName(model, "C", "Zone"));
        Assert.Equal("Zone", DiagramPlacement.UniqueShortName(model, "", "Zone"));
    }
}
