using Dependinator.UI.Modeling;
using Dependinator.UI.Modeling.Commands;
using Dependinator.UI.Modeling.Models;
using Dependinator.UI.Shared;
using Dependinator.UI.Shared.Types;

namespace Dependinator.UI.Tests.Commands;

public class ManualCommandTests
{
    static IModel CreateModel()
    {
        var model = new ModelMgr(new StateMgr()).UseModel();
        model.UpdateStamp = new DateTime(2024, 1, 1);
        return model;
    }

    static StructureService CreateStructureService() => new(new Mock<ILineService>().Object);

    static void AddRootNode(IModel model, string name)
    {
        var node = new Node(name, model.Root) { UpdateStamp = model.UpdateStamp };
        model.Root.AddChild(node);
        model.TryAddNode(node);
    }

    [Fact]
    public void AddNodeCommand_ShouldAddManualNode_AndRevertRemoveIt()
    {
        using var model = CreateModel();
        var command = new AddNodeCommand("MyNode", model.Root.Name, new Rect(0, 0, 80, 40));

        command.Execute(model);

        Assert.True(model.Nodes.TryGetValue(NodeId.FromName("MyNode"), out var node));
        Assert.True(node!.IsManual);
        Assert.Contains(node, model.Root.Children);

        command.Revert(model);
        Assert.False(model.Nodes.ContainsKey(NodeId.FromName("MyNode")));
    }

    [Fact]
    public void AddLinkCommand_ShouldAddManualLink_AndRevertRemoveIt()
    {
        using var model = CreateModel();
        AddRootNode(model, "A");
        AddRootNode(model, "B");
        var command = new AddLinkCommand(CreateStructureService(), "A", "B");

        command.Execute(model);

        Assert.True(model.Links.TryGetValue(new LinkId("A", "B"), out var link));
        Assert.True(link!.IsManual);

        command.Revert(model);
        Assert.False(model.Links.ContainsKey(new LinkId("A", "B")));
    }

    [Fact]
    public void RenameNodeCommand_ShouldRenameNode_PreserveProperties_AndRevert()
    {
        using var model = CreateModel();
        new AddNodeCommand("Old", model.Root.Name, new Rect(20, 30, 100, 100)).Execute(model);

        var command = new RenameNodeCommand(CreateStructureService(), "Old", "New");
        command.Execute(model);

        Assert.False(model.Nodes.ContainsKey(NodeId.FromName("Old")));
        Assert.True(model.Nodes.TryGetValue(NodeId.FromName("New"), out var renamed));
        Assert.True(renamed!.IsManual);
        Assert.Equal(new Rect(20, 30, 100, 100), renamed.Boundary);
        Assert.Contains(renamed, model.Root.Children);

        command.Revert(model);
        Assert.True(model.Nodes.ContainsKey(NodeId.FromName("Old")));
        Assert.False(model.Nodes.ContainsKey(NodeId.FromName("New")));
    }

    [Fact]
    public void RenameNodeCommand_ShouldMigrateLinks_ToRenamedNode()
    {
        using var model = CreateModel();
        AddRootNode(model, "A");
        AddRootNode(model, "B");
        var structureService = CreateStructureService();
        structureService.AddManualLink(model, "A", "B");

        new RenameNodeCommand(structureService, "A", "C").Execute(model);

        Assert.False(model.Links.ContainsKey(new LinkId("A", "B")));
        Assert.True(model.Links.TryGetValue(new LinkId("C", "B"), out var link));
        Assert.True(link!.IsManual);
    }

    [Fact]
    public void RenameNode_ShouldBeNoOp_WhenTargetNameTaken()
    {
        using var model = CreateModel();
        AddRootNode(model, "A");
        AddRootNode(model, "B");

        CreateStructureService().RenameNode(model, "A", "B");

        // Both original nodes remain unchanged.
        Assert.True(model.Nodes.ContainsKey(NodeId.FromName("A")));
        Assert.True(model.Nodes.ContainsKey(NodeId.FromName("B")));
    }

    [Fact]
    public void DeleteNodeCommand_ShouldRemoveManualNode_AndRevertRestoreIt()
    {
        using var model = CreateModel();
        var add = new AddNodeCommand("MyNode", model.Root.Name, new Rect(0, 0, 80, 40));
        add.Execute(model);

        var delete = new DeleteNodeCommand(NodeId.FromName("MyNode"));
        delete.Execute(model);
        Assert.False(model.Nodes.ContainsKey(NodeId.FromName("MyNode")));

        delete.Revert(model);
        Assert.True(model.Nodes.TryGetValue(NodeId.FromName("MyNode"), out var restored));
        Assert.True(restored!.IsManual);
        Assert.Contains(restored, model.Root.Children);
    }
}
