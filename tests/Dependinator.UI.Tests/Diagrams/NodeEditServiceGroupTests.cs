using Dependinator.Core.Parsing;
using Dependinator.UI.Diagrams.Interaction;
using Dependinator.UI.Modeling;
using Dependinator.UI.Modeling.Commands;
using Dependinator.UI.Modeling.Models;
using Dependinator.UI.Shared;
using Dependinator.UI.Shared.Types;
using Node = Dependinator.UI.Modeling.Models.Node;

namespace Dependinator.UI.Tests.Diagrams;

// Group edits: moving a selection moves every node by the pointer delta in its own container's
// scale, and the whole group edit is one undo step; so are group size and color changes.
public class NodeEditServiceGroupTests
{
    readonly ModelMgr modelMgr = new(new StateMgr());
    readonly CommandService commandService;
    readonly NodeEditService service;

    public NodeEditServiceGroupTests()
    {
        commandService = new CommandService(Mock.Of<IApplicationEvents>(), modelMgr);
        service = new NodeEditService(modelMgr, commandService);
    }

    static Node AddNode(IModel model, string name, Node parent, double containerZoom = 1)
    {
        var node = new Node(name, parent)
        {
            Boundary = new Rect(100, 100, 100, 100),
            Type = NodeType.ClassType,
            ContainerZoom = containerZoom,
        };
        parent.AddChild(node);
        model.TryAddNode(node);
        return node;
    }

    [Fact]
    public async Task MoveSelectedNodes_ShouldMoveEachInItsOwnScale_AsOneUndoStep()
    {
        Node a,
            b;
        using (var model = modelMgr.UseModel())
        {
            var parentA = AddNode(model, "PA", model.Root, containerZoom: 1);
            var parentB = AddNode(model, "PB", model.Root, containerZoom: 0.5);
            a = AddNode(model, "PA.A", parentA);
            b = AddNode(model, "PB.B", parentB);
        }
        var rootZoom = modelMgr.WithModel(m => m.Root.ContainerZoom);

        service.MoveSelectedNodes(new PointerEvent { MovementX = 10, MovementY = 0 }, zoom: 1, [a.Id, b.Id]);

        // B's container is zoomed to half, so the same screen delta is twice as far in its space.
        Assert.Equal(100 + 10 * rootZoom, a.Boundary.X, 6);
        Assert.Equal(100 + 20 * rootZoom, b.Boundary.X, 6);
        Assert.True(commandService.CanUndo);

        await commandService.Undo();
        Assert.Equal(100, a.Boundary.X, 6);
        Assert.Equal(100, b.Boundary.X, 6);
        Assert.False(commandService.CanUndo);
    }

    [Fact]
    public async Task GroupSizeAndColor_ShouldApplyToAll_AsOneUndoStep()
    {
        Node a,
            b;
        using (var model = modelMgr.UseModel())
        {
            a = AddNode(model, "A", model.Root);
            b = AddNode(model, "B", model.Root);
        }

        service.IncreaseNodeSize([a.Id, b.Id]);
        Assert.True(a.Boundary.Width > 100 && b.Boundary.Width > 100);
        await commandService.Undo();
        Assert.Equal(100, a.Boundary.Width);
        Assert.Equal(100, b.Boundary.Width);

        service.SetNodeColor([a.Id, b.Id], "Red");
        Assert.Equal("Red", a.CustomColor);
        Assert.Equal("Red", b.CustomColor);
        service.SetNodeColor([a.Id, b.Id], "Red"); // Already red: nothing to do, no undo step added
        await commandService.Undo();
        Assert.Null(a.CustomColor);
        Assert.Null(b.CustomColor);
        Assert.False(commandService.CanUndo);
    }
}
