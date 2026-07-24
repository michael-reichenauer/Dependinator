using Dependinator.UI.Diagrams;
using Dependinator.UI.Diagrams.Interaction;
using Dependinator.UI.Modeling;
using Dependinator.UI.Modeling.Commands;
using Dependinator.UI.Modeling.Models;
using Dependinator.UI.Shared;
using Dependinator.UI.Shared.Types;
using MudBlazor;

namespace Dependinator.UI.Tests.Diagrams;

public class ManualEditServiceLinkDragTests
{
    readonly ModelMgr modelMgr = new(new StateMgr());
    readonly Mock<ICommandService> commandService = new();
    readonly Mock<IDialogService> dialogService = new();

    ManualEditService CreateService() =>
        new(
            modelMgr,
            commandService.Object,
            new StructureService(new Mock<ILineService>().Object),
            new Mock<ISelectionService>().Object,
            dialogService.Object,
            new Mock<IApplicationEvents>().Object
        );

    NodeId AddRootNode(string name)
    {
        using var model = modelMgr.UseModel();
        var node = new Node(name, model.Root) { UpdateStamp = model.UpdateStamp };
        model.Root.AddChild(node);
        model.TryAddNode(node);
        return node.Id;
    }

    // The icon selector dialog resolving to the given result (DialogResult.Ok(icon) or Cancel).
    void SetupIconDialog(DialogResult? result)
    {
        var reference = new Mock<IDialogReference>();
        reference.SetupGet(r => r.Result).Returns(Task.FromResult(result));
        dialogService
            .Setup(d => d.ShowAsync<IconSelectorDialog>(null, It.IsAny<DialogParameters>(), It.IsAny<DialogOptions>()))
            .ReturnsAsync(reference.Object);
    }

    [Fact]
    public void BeginLinkDrag_ShouldActivate_AndTrackPositions()
    {
        var sourceId = AddRootNode("A");
        var service = CreateService();

        service.BeginLinkDrag(sourceId, new Pos(10, 20));

        Assert.True(service.IsLinkDragActive);
        Assert.Equal(new Pos(10, 20), service.LinkDragStart);
        Assert.Equal(new Pos(10, 20), service.LinkDragEnd);

        service.UpdateLinkDrag(new Pos(50, 60));
        Assert.Equal(new Pos(10, 20), service.LinkDragStart);
        Assert.Equal(new Pos(50, 60), service.LinkDragEnd);
    }

    [Fact]
    public void BeginLinkDrag_ShouldNotActivate_ForUnknownNode()
    {
        var service = CreateService();

        service.BeginLinkDrag(NodeId.FromName("Unknown"), new Pos(10, 20));

        Assert.False(service.IsLinkDragActive);
    }

    [Fact]
    public async Task CompleteLinkDragAsync_ShouldCreateLink_WhenDroppedOnIconNode()
    {
        var sourceId = AddRootNode("A");
        var targetId = AddRootNode("B");
        var service = CreateService();
        service.BeginLinkDrag(sourceId, new Pos(10, 20));

        await service.CompleteLinkDragAsync(PointerId.Parse(PointerId.FromNode(targetId).ElementId), new Pos(30, 40));

        Assert.False(service.IsLinkDragActive);
        commandService.Verify(c => c.Do(It.IsAny<AddLinkCommand>()), Times.Once);
        dialogService.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task CompleteLinkDragAsync_ShouldNotCreateLink_WhenDroppedOnSelf()
    {
        var sourceId = AddRootNode("A");
        var service = CreateService();
        service.BeginLinkDrag(sourceId, new Pos(10, 20));

        await service.CompleteLinkDragAsync(PointerId.Parse(PointerId.FromNode(sourceId).ElementId), new Pos(30, 40));

        Assert.False(service.IsLinkDragActive);
        commandService.Verify(c => c.Do(It.IsAny<Command>()), Times.Never);
    }

    [Fact]
    public async Task CompleteLinkDragAsync_ShouldDoNothing_WhenDroppedOutsideDiagram()
    {
        var sourceId = AddRootNode("A");
        var service = CreateService();
        service.BeginLinkDrag(sourceId, new Pos(10, 20));

        await service.CompleteLinkDragAsync(PointerId.Empty, new Pos(30, 40));

        Assert.False(service.IsLinkDragActive);
        commandService.Verify(c => c.Do(It.IsAny<Command>()), Times.Never);
        dialogService.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task CompleteLinkDragAsync_ShouldAddNodeAndLink_WhenDroppedOnCanvas()
    {
        var sourceId = AddRootNode("A");
        var service = CreateService();
        SetupIconDialog(DialogResult.Ok("Database"));
        Command? done = null;
        commandService
            .Setup(c => c.Do(It.IsAny<Command>(), It.IsAny<bool>(), It.IsAny<bool>()))
            .Callback<Command, bool, bool>((c, _, _) => done = c);
        service.BeginLinkDrag(sourceId, new Pos(10, 20));

        await service.CompleteLinkDragAsync(PointerId.Parse(PointerId.CanvasElementId), new Pos(30, 40));

        Assert.False(service.IsLinkDragActive);
        // One undoable step creating the new node and the link to it.
        var composite = Assert.IsType<CompositeCommand>(done);
        Assert.Collection(
            composite.Commands,
            c => Assert.IsType<AddNodeCommand>(c),
            c => Assert.IsType<AddLinkCommand>(c)
        );
    }

    [Fact]
    public async Task CompleteLinkDragAsync_ShouldNotAddNode_WhenIconDialogCanceled()
    {
        var sourceId = AddRootNode("A");
        var service = CreateService();
        SetupIconDialog(DialogResult.Cancel());
        service.BeginLinkDrag(sourceId, new Pos(10, 20));

        await service.CompleteLinkDragAsync(PointerId.Parse(PointerId.CanvasElementId), new Pos(30, 40));

        Assert.False(service.IsLinkDragActive);
        commandService.Verify(c => c.Do(It.IsAny<Command>()), Times.Never);
    }

    [Fact]
    public async Task CompleteLinkDragAsync_ShouldNotAddNode_WhenCanvasPositionIsUnknown()
    {
        var sourceId = AddRootNode("A");
        var service = CreateService();
        service.BeginLinkDrag(sourceId, new Pos(10, 20));

        await service.CompleteLinkDragAsync(PointerId.Parse(PointerId.CanvasElementId), null);

        Assert.False(service.IsLinkDragActive);
        commandService.Verify(c => c.Do(It.IsAny<Command>()), Times.Never);
        dialogService.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task CompleteLinkDragAsync_ShouldDoNothing_WhenNoDragActive()
    {
        var targetId = AddRootNode("B");
        var service = CreateService();

        await service.CompleteLinkDragAsync(PointerId.Parse(PointerId.FromNode(targetId).ElementId), new Pos(30, 40));

        commandService.Verify(c => c.Do(It.IsAny<Command>()), Times.Never);
    }

    [Fact]
    public void CancelLinkDrag_ShouldReset_WithoutCreatingLink()
    {
        var sourceId = AddRootNode("A");
        var service = CreateService();
        service.BeginLinkDrag(sourceId, new Pos(10, 20));

        service.CancelLinkDrag();

        Assert.False(service.IsLinkDragActive);
        Assert.Equal(Pos.None, service.LinkDragStart);
        Assert.Equal(Pos.None, service.LinkDragEnd);
        commandService.Verify(c => c.Do(It.IsAny<Command>()), Times.Never);
    }
}
