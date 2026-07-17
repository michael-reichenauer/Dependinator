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

    ManualEditService CreateService() =>
        new(
            modelMgr,
            commandService.Object,
            new StructureService(new Mock<ILineService>().Object),
            new Mock<ISelectionService>().Object,
            new Mock<IDialogService>().Object
        );

    NodeId AddRootNode(string name)
    {
        using var model = modelMgr.UseModel();
        var node = new Node(name, model.Root) { UpdateStamp = model.UpdateStamp };
        model.Root.AddChild(node);
        model.TryAddNode(node);
        return node.Id;
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
    public void TryCompleteLinkDrag_ShouldCreateLink_WhenDroppedOnOtherNode()
    {
        var sourceId = AddRootNode("A");
        var targetId = AddRootNode("B");
        var service = CreateService();
        service.BeginLinkDrag(sourceId, new Pos(10, 20));

        var result = service.TryCompleteLinkDrag(PointerId.Parse(PointerId.FromNode(targetId).ElementId));

        Assert.True(result);
        Assert.False(service.IsLinkDragActive);
        commandService.Verify(c => c.Do(It.IsAny<AddLinkCommand>()), Times.Once);
    }

    [Fact]
    public void TryCompleteLinkDrag_ShouldNotCreateLink_WhenDroppedOnSelf()
    {
        var sourceId = AddRootNode("A");
        var service = CreateService();
        service.BeginLinkDrag(sourceId, new Pos(10, 20));

        var result = service.TryCompleteLinkDrag(PointerId.Parse(PointerId.FromNode(sourceId).ElementId));

        Assert.False(result);
        Assert.False(service.IsLinkDragActive);
        commandService.Verify(c => c.Do(It.IsAny<Command>()), Times.Never);
    }

    [Fact]
    public void TryCompleteLinkDrag_ShouldNotCreateLink_WhenDroppedOnNonNode()
    {
        var sourceId = AddRootNode("A");
        var service = CreateService();
        service.BeginLinkDrag(sourceId, new Pos(10, 20));

        var result = service.TryCompleteLinkDrag(PointerId.Empty);

        Assert.False(result);
        Assert.False(service.IsLinkDragActive);
        commandService.Verify(c => c.Do(It.IsAny<Command>()), Times.Never);
    }

    [Fact]
    public void TryCompleteLinkDrag_ShouldDoNothing_WhenNoDragActive()
    {
        var targetId = AddRootNode("B");
        var service = CreateService();

        var result = service.TryCompleteLinkDrag(PointerId.Parse(PointerId.FromNode(targetId).ElementId));

        Assert.False(result);
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

    [Fact]
    public void LinkDrag_ShouldNotActivate_ClickBasedAddLinkMode()
    {
        var sourceId = AddRootNode("A");
        var service = CreateService();

        service.BeginLinkDrag(sourceId, new Pos(10, 20));

        Assert.False(service.IsAddingLink);
    }
}
