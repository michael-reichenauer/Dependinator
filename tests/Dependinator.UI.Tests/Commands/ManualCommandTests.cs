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
    public void AddNodeCommand_ShouldSetCustomIcon_WhenIconNameGiven()
    {
        using var model = CreateModel();
        var command = new AddNodeCommand("MyNode", model.Root.Name, new Rect(0, 0, 80, 40), iconName: "Database");

        command.Execute(model);

        Assert.True(model.Nodes.TryGetValue(NodeId.FromName("MyNode"), out var node));
        Assert.Equal("Database", node!.CustomIconName);

        command.Revert(model);
        Assert.False(model.Nodes.ContainsKey(NodeId.FromName("MyNode")));
    }

    [Fact]
    public void ParentQualifiedNames_AllowSameShortName_UnderDifferentParents()
    {
        using var model = CreateModel();
        new AddNodeCommand("A", model.Root.Name, new Rect(0, 0, 100, 100)).Execute(model);
        new AddNodeCommand("B", model.Root.Name, new Rect(0, 0, 100, 100)).Execute(model);

        // Same short name ("Child") under two different parents => distinct full-name identities.
        new AddNodeCommand("A.Child", "A", new Rect(0, 0, 40, 40)).Execute(model);
        new AddNodeCommand("B.Child", "B", new Rect(0, 0, 40, 40)).Execute(model);

        Assert.True(model.Nodes.TryGetValue(NodeId.FromName("A.Child"), out var underA));
        Assert.True(model.Nodes.TryGetValue(NodeId.FromName("B.Child"), out var underB));
        Assert.NotEqual(underA!.Id, underB!.Id);
        // Both display just the short name typed in the text box.
        Assert.Equal("Child", underA.ShortName);
        Assert.Equal("Child", underB.ShortName);
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
    public void RenameNode_ShouldRenameSubtree_RemapLinks_AndRevert()
    {
        using var model = CreateModel();
        var structureService = CreateStructureService();
        new AddNodeCommand("A", model.Root.Name, new Rect(0, 0, 100, 100)).Execute(model);
        new AddNodeCommand("A.B", "A", new Rect(0, 0, 40, 40)).Execute(model);
        new AddNodeCommand("Ext", model.Root.Name, new Rect(0, 0, 100, 100)).Execute(model);
        structureService.AddManualLink(model, "A", "A.B"); // internal (parent -> child)
        structureService.AddManualLink(model, "A.B", "Ext"); // subtree -> external
        structureService.AddManualLink(model, "Ext", "A"); // external -> subtree

        var command = new RenameNodeCommand(structureService, "A", "X");
        command.Execute(model);

        // The whole subtree is re-prefixed; the child keeps its short name under the new parent.
        Assert.False(model.Nodes.ContainsKey(NodeId.FromName("A")));
        Assert.False(model.Nodes.ContainsKey(NodeId.FromName("A.B")));
        Assert.True(model.Nodes.TryGetValue(NodeId.FromName("X"), out var x));
        Assert.True(model.Nodes.TryGetValue(NodeId.FromName("X.B"), out var xb));
        Assert.Equal(x, xb!.Parent);
        Assert.Equal("B", xb.ShortName);

        // Links to/from the node and its child are remapped (external endpoints unchanged).
        Assert.True(model.Links.ContainsKey(new LinkId("X", "X.B")));
        Assert.True(model.Links.ContainsKey(new LinkId("X.B", "Ext")));
        Assert.True(model.Links.ContainsKey(new LinkId("Ext", "X")));
        Assert.False(model.Links.ContainsKey(new LinkId("A", "A.B")));
        Assert.False(model.Links.ContainsKey(new LinkId("A.B", "Ext")));
        Assert.False(model.Links.ContainsKey(new LinkId("Ext", "A")));

        command.Revert(model);
        Assert.True(model.Nodes.ContainsKey(NodeId.FromName("A")));
        Assert.True(model.Nodes.ContainsKey(NodeId.FromName("A.B")));
        Assert.False(model.Nodes.ContainsKey(NodeId.FromName("X")));
        Assert.True(model.Links.ContainsKey(new LinkId("A", "A.B")));
        Assert.True(model.Links.ContainsKey(new LinkId("A.B", "Ext")));
        Assert.True(model.Links.ContainsKey(new LinkId("Ext", "A")));
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
    public void DescendantsAndSelfPostOrder_ShouldReturnChildrenBeforeParent()
    {
        using var model = CreateModel();
        new AddNodeCommand("P", model.Root.Name, new Rect(0, 0, 100, 100)).Execute(model);
        new AddNodeCommand("C1", "P", new Rect(0, 0, 40, 40)).Execute(model);
        new AddNodeCommand("C2", "P", new Rect(0, 0, 40, 40)).Execute(model);

        var parent = model.Nodes[NodeId.FromName("P")];
        var order = parent.DescendantsAndSelfPostOrder().Select(n => n.Name).ToList();

        Assert.Equal(["C1", "C2", "P"], order);
    }

    [Fact]
    public void CascadeDelete_ShouldRemoveParentChildAndLink_AndRevertRestoreTree()
    {
        using var model = CreateModel();
        new AddNodeCommand("P", model.Root.Name, new Rect(0, 0, 100, 100)).Execute(model);
        new AddNodeCommand("C", "P", new Rect(10, 10, 40, 40)).Execute(model);
        var structureService = CreateStructureService();
        structureService.AddManualLink(model, "P", "C");

        // Post-order composite (child + its link before the parent), as DeleteManualNode builds.
        var composite = new CompositeCommand(
            new DeleteLinkCommand(structureService, "P", "C"),
            new DeleteNodeCommand(NodeId.FromName("C")),
            new DeleteNodeCommand(NodeId.FromName("P"))
        );

        composite.Execute(model);
        Assert.False(model.Nodes.ContainsKey(NodeId.FromName("P")));
        Assert.False(model.Nodes.ContainsKey(NodeId.FromName("C")));
        Assert.False(model.Links.ContainsKey(new LinkId("P", "C")));

        composite.Revert(model);
        Assert.True(model.Nodes.TryGetValue(NodeId.FromName("P"), out var parent));
        Assert.True(model.Nodes.TryGetValue(NodeId.FromName("C"), out var child));
        Assert.Equal(parent, child!.Parent);
        Assert.Contains(child, parent!.Children);
        Assert.True(model.Links.ContainsKey(new LinkId("P", "C")));
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
