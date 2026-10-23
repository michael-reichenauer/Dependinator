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
        service = new HiddenNodeService(
            modelMgr,
            commandService,
            new ViewOptions(),
            Mock.Of<IPathFinderService>(),
            Mock.Of<IApplicationEvents>()
        );
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

    [Fact]
    public void GetHiddenChildCounts_ShouldCountTheHiddenChildrenOfEachParent_AndKeepTheInstanceWhileUnchanged()
    {
        var parent = AddNode("P");
        var x = AddNode("P.X", parent);
        var y = AddNode("P.Y", parent);
        var z = AddNode("P.Z", parent);
        service.SetVisibility(x.Id, NodeVisibility.Hidden);
        service.SetVisibility(y.Id, NodeVisibility.Hidden);
        service.SetVisibility(z.Id, NodeVisibility.Dimmed); // still drawn, not counted

        var counts = service.GetHiddenChildCounts();
        Assert.Equal(2, counts[parent.Id]);
        Assert.Single(counts);

        // A change that leaves the counts as they were keeps the instance (tiles stay cached).
        service.SetVisibility(z.Id, NodeVisibility.Visible);
        Assert.Same(counts, service.GetHiddenChildCounts());

        service.SetVisibility(x.Id, NodeVisibility.Visible);
        Assert.Equal(1, service.GetHiddenChildCounts()[parent.Id]);
    }

    [Fact]
    public void OpenPanel_ShouldListTheGivenParent_AndTogglePanelClose()
    {
        var viewOptions = new ViewOptions();
        var panelService = new HiddenNodeService(
            modelMgr,
            commandService,
            viewOptions,
            Mock.Of<IPathFinderService>(),
            Mock.Of<IApplicationEvents>()
        );
        var parent = AddNode("P");
        viewOptions.SetRulesShown(true);

        panelService.OpenPanel(parent.Id);

        Assert.True(viewOptions.IsHiddenNodesShown);
        Assert.Equal(parent.Id, viewOptions.HiddenNodesParentId);
        Assert.False(viewOptions.IsRulesShown); // Same corner

        // Toggling a parent's list switches to the whole model, and then closes it.
        panelService.TogglePanel();
        Assert.True(viewOptions.IsHiddenNodesShown);
        Assert.Null(viewOptions.HiddenNodesParentId);
        panelService.TogglePanel();
        Assert.False(viewOptions.IsHiddenNodesShown);
    }

    [Fact]
    public void GetPanelScope_ShouldEnd_WhenTheParentIsGoneOrAnotherModelIsOpen()
    {
        var viewOptions = new ViewOptions();
        var panelService = new HiddenNodeService(
            modelMgr,
            commandService,
            viewOptions,
            Mock.Of<IPathFinderService>(),
            Mock.Of<IApplicationEvents>()
        );
        var parent = AddNode("P");
        modelMgr.WithModel(m => m.Path = "a.sln");

        panelService.OpenPanel(parent.Id);
        Assert.Equal((parent.Id, "P"), panelService.GetPanelScope());

        // Another model with a node of the same name (ids are names) is not the same parent.
        modelMgr.WithModel(m => m.Path = "b.sln");
        Assert.Null(panelService.GetPanelScope());
        Assert.True(viewOptions.IsHiddenNodesShown); // the panel stays, on the whole model
        Assert.Null(viewOptions.HiddenNodesParentId);

        panelService.OpenPanel(parent.Id);
        modelMgr.WithModel(m => m.RemoveNode(parent));
        Assert.Null(panelService.GetPanelScope());
    }
}
