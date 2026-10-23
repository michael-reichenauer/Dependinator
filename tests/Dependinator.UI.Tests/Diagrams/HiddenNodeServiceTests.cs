using Dependinator.Core.Parsing;
using Dependinator.UI.Diagrams;
using Dependinator.UI.Modeling;
using Dependinator.UI.Modeling.Commands;
using Dependinator.UI.Modeling.Models;
using Dependinator.UI.Shared;
using Dependinator.UI.Shared.Types;
using Node = Dependinator.UI.Modeling.Models.Node;

namespace Dependinator.UI.Tests.Diagrams;

// The hidden nodes list: only the nodes the user dimmed or hid themselves, sorted by full name,
// kept current after each change, Show as one undo step, and no undo step for the state a
// node already has.
public class HiddenNodeServiceTests
{
    readonly ModelMgr modelMgr = new(new StateMgr());
    readonly CommandService commandService;
    readonly HiddenNodeService service;

    public HiddenNodeServiceTests()
    {
        commandService = new CommandService(Mock.Of<IApplicationEvents>(), modelMgr);
        service = new HiddenNodeService(modelMgr, commandService);
    }

    Node AddNode(string name, Node? parent = null) =>
        modelMgr.WithModel(m =>
        {
            parent ??= m.Root;
            var node = new Node(name, parent) { Type = NodeType.ClassType, Boundary = new Rect(0, 0, 100, 100) };
            parent.AddChild(node);
            m.TryAddNode(node);
            return node;
        });

    [Fact]
    public void GetNodes_ShouldListOnlyTheNodesTheUserDimmedOrHid_SortedByName()
    {
        var b = AddNode("B");
        var a = AddNode("A");
        var child = AddNode("B.X", b);
        AddNode("C");
        Assert.Empty(service.GetNodes());

        service.SetVisibility(b.Id, NodeVisibility.Hidden);
        service.SetVisibility(a.Id, NodeVisibility.Dimmed);

        var nodes = service.GetNodes();
        Assert.Equal(["A", "B"], nodes.Select(n => n.Name));
        Assert.Equal(NodeVisibility.Dimmed, nodes[0].Visibility);
        Assert.Equal(NodeVisibility.Hidden, nodes[1].Visibility);
        Assert.Equal(2, service.Count);
        Assert.Equal(NodeVisibility.Hidden, child.Visibility); // follows B, but not listed
    }

    [Fact]
    public void GetNodes_ShouldMarkANodeInsideAHiddenOne_AsFollowingIt()
    {
        var parent = AddNode("P");
        var child = AddNode("P.X", parent);
        service.SetVisibility(child.Id, NodeVisibility.Dimmed);
        service.SetVisibility(parent.Id, NodeVisibility.Hidden);

        var row = service.GetNodes().Single(n => n.Id == child.Id);

        Assert.True(row.IsParentNotVisible);
        Assert.Equal("P", row.ParentName);
        Assert.False(service.GetNodes().Single(n => n.Id == parent.Id).IsParentNotVisible);
    }

    [Fact]
    public async Task Show_ShouldShowTheGivenNodes_AsOneUndoStep()
    {
        var a = AddNode("A");
        var b = AddNode("B");
        service.SetVisibility(a.Id, NodeVisibility.Dimmed);
        service.SetVisibility(b.Id, NodeVisibility.Hidden);

        service.Show([a.Id, b.Id]);

        Assert.True(a.IsVisible && b.IsVisible);
        Assert.Empty(service.GetNodes());

        await commandService.Undo();
        Assert.Equal(2, service.Count);
        Assert.Equal(NodeVisibility.Hidden, b.Visibility);
    }

    [Fact]
    public async Task SetVisibility_ShouldAddNoUndoStep_WhenTheNodeAlreadyHasIt()
    {
        var a = AddNode("A");
        service.SetVisibility(a.Id, NodeVisibility.Hidden);

        service.SetVisibility(a.Id, NodeVisibility.Hidden);

        await commandService.Undo();
        Assert.True(a.IsVisible);
        Assert.False(commandService.CanUndo);
    }
}
