using Dependinator.Core.Parsing;
using Dependinator.UI.Diagrams.Interaction;
using Dependinator.UI.Modeling;
using Dependinator.UI.Modeling.Models;
using Dependinator.UI.Shared;
using Dependinator.UI.Shared.Types;
using Node = Dependinator.UI.Modeling.Models.Node;

namespace Dependinator.UI.Tests.Diagrams;

// Group selection: Shift/Ctrl+click adds a node to the selection (and removes it again), the
// primary node stays the toolbar's anchor, hiding applies to the whole group, and clearing the
// selection clears every node's selected flag.
public class SelectionServiceTests
{
    readonly ModelMgr modelMgr = new(new StateMgr());
    readonly Mock<IScreenService> screenService = new();
    readonly Mock<IApplicationEvents> applicationEvents = new();
    readonly Mock<IModelService> modelService = new();

    public SelectionServiceTests()
    {
        screenService.Setup(s => s.SvgRect).Returns(new Rect(0, 0, 800, 600));
        screenService
            .Setup(s => s.GetBoundingRectangle(It.IsAny<string>()))
            .ReturnsAsync((Result<ElementBoundingRectangle>)new Error("Not on screen"));
        modelMgr.WithModel(m => m.Zoom = 1);
    }

    static Node AddNode(IModel model, string name, Node parent)
    {
        var node = new Node(name, parent) { Boundary = new Rect(0, 0, 100, 100), Type = NodeType.ClassType };
        parent.AddChild(node);
        model.TryAddNode(node);
        return node;
    }

    SelectionService CreateService() =>
        new(modelMgr, modelService.Object, applicationEvents.Object, screenService.Object);

    static PointerEvent ShiftClick => new() { ShiftKey = true };

    [Fact]
    public async Task ToggleInSelection_ShouldAddAndRemoveNodes_AroundThePrimaryOne()
    {
        Node a,
            b,
            c;
        using (var model = modelMgr.UseModel())
        {
            a = AddNode(model, "A", model.Root);
            b = AddNode(model, "B", model.Root);
            c = AddNode(model, "C", model.Root);
        }
        var service = CreateService();

        await service.Select(a.Id);
        await service.ToggleInSelectionAsync(PointerId.FromNode(b.Id), ShiftClick);
        await service.ToggleInSelectionAsync(PointerId.FromNode(c.Id), ShiftClick);

        Assert.Equal(PointerId.FromNode(a.Id), service.SelectedId);
        Assert.Equal([a.Id, b.Id, c.Id], service.SelectedNodeIds);
        Assert.Equal(3, service.SelectedNodeCount);
        Assert.True(b.IsSelected && c.IsSelected);

        await service.ToggleInSelectionAsync(PointerId.FromNode(b.Id), ShiftClick);
        Assert.Equal([a.Id, c.Id], service.SelectedNodeIds);
        Assert.False(b.IsSelected);

        // Shift+clicking the primary node keeps the selection as it is.
        await service.ToggleInSelectionAsync(PointerId.FromNode(a.Id), ShiftClick);
        Assert.Equal([a.Id, c.Id], service.SelectedNodeIds);

        service.Unselect();
        Assert.Empty(service.SelectedNodeIds);
        Assert.False(a.IsSelected || c.IsSelected);
    }

    [Fact]
    public async Task ToggleInSelection_ShouldSelectNormally_WhenNothingIsSelected()
    {
        Node a;
        using (var model = modelMgr.UseModel())
        {
            a = AddNode(model, "A", model.Root);
        }
        var service = CreateService();

        await service.ToggleInSelectionAsync(PointerId.FromNode(a.Id), ShiftClick);

        Assert.Equal([a.Id], service.SelectedNodeIds);
        Assert.True(a.IsSelected);
    }

    [Fact]
    public async Task ToggleNodeHide_ShouldHideTheWholeGroup_AndShowItAgain()
    {
        Node a,
            b;
        using (var model = modelMgr.UseModel())
        {
            a = AddNode(model, "A", model.Root);
            b = AddNode(model, "B", model.Root);
        }
        var service = CreateService();
        await service.Select(a.Id);
        await service.ToggleInSelectionAsync(PointerId.FromNode(b.Id), ShiftClick);

        service.ToggleNodeHide();
        Assert.True(a.IsHidden && b.IsHidden);

        service.ToggleNodeHide();
        Assert.False(a.IsHidden || b.IsHidden);
    }

    [Fact]
    public async Task Select_ShouldReplaceAGroupSelection()
    {
        Node a,
            b;
        using (var model = modelMgr.UseModel())
        {
            a = AddNode(model, "A", model.Root);
            b = AddNode(model, "B", model.Root);
        }
        var service = CreateService();
        await service.Select(a.Id);
        await service.ToggleInSelectionAsync(PointerId.FromNode(b.Id), ShiftClick);

        await service.Select(b.Id);

        Assert.Equal([b.Id], service.SelectedNodeIds);
        Assert.False(a.IsSelected);
        Assert.True(b.IsSelected);
    }
}
