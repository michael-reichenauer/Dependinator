using Dependinator.Core.Parsing;
using Dependinator.UI.Modeling;
using Dependinator.UI.Modeling.Commands;
using Dependinator.UI.Modeling.Models;
using Dependinator.UI.Shared;
using Dependinator.UI.Shared.Types;
using Link = Dependinator.UI.Modeling.Models.Link;
using Node = Dependinator.UI.Modeling.Models.Node;

namespace Dependinator.UI.Tests.Commands;

// Dimming and hiding are undoable edits: the nodes' own states are restored on revert, the
// descendants follow the outermost dimmed or hidden ancestor, a child's own state applies again
// once its parent is shown, and the lines follow the nodes.
public class SetNodeVisibilityCommandTests
{
    static Node AddNode(IModel model, string name, Node parent)
    {
        var node = new Node(name, parent) { Type = NodeType.ClassType, Boundary = new Rect(0, 0, 100, 100) };
        parent.AddChild(node);
        model.TryAddNode(node);
        return node;
    }

    static Line AddLink(IModel model, Node source, Node target)
    {
        var link = new Link(source, target);
        model.TryAddLink(link);
        source.AddSourceLink(link);
        target.AddTargetLink(link);
        new LineService().AddLinesFromSourceToTarget(model, link);
        return model.Lines[LineId.From(source.Name, target.Name)];
    }

    [Fact]
    public void Execute_ShouldHideNodesAndTheirLines_AndRevertRestoreThem()
    {
        using var model = new ModelMgr(new StateMgr()).UseModel();
        var a = AddNode(model, "A", model.Root);
        var b = AddNode(model, "B", model.Root);
        var c = AddNode(model, "C", model.Root);
        var child = AddNode(model, "A.X", a);
        var line = AddLink(model, a, b);
        var otherLine = AddLink(model, b, c);

        var command = new SetNodeVisibilityCommand([a.Id, b.Id], NodeVisibility.Hidden);
        command.Execute(model);

        Assert.Equal(NodeVisibility.Hidden, a.UserVisibility);
        Assert.Equal(NodeVisibility.Hidden, b.UserVisibility);
        Assert.Equal(NodeVisibility.Hidden, child.Visibility);
        Assert.Equal(NodeVisibility.Visible, child.UserVisibility);
        Assert.Equal(NodeVisibility.Hidden, line.Visibility);
        Assert.Equal(NodeVisibility.Hidden, otherLine.Visibility); // one end hidden
        Assert.True(c.IsVisible);

        command.Revert(model);

        Assert.True(a.IsVisible && b.IsVisible && child.IsVisible);
        Assert.True(line.IsVisible && otherLine.IsVisible);
    }

    [Fact]
    public void Execute_ShouldDimLines_WhenAnEndIsDimmedAndTheOtherHidden()
    {
        using var model = new ModelMgr(new StateMgr()).UseModel();
        var a = AddNode(model, "A", model.Root);
        var b = AddNode(model, "B", model.Root);
        var c = AddNode(model, "C", model.Root);
        var ab = AddLink(model, a, b);
        var bc = AddLink(model, b, c);

        new SetNodeVisibilityCommand([a.Id], NodeVisibility.Dimmed).Execute(model);
        new SetNodeVisibilityCommand([b.Id], NodeVisibility.Hidden).Execute(model);

        // A line goes as far as its stronger end: A-B is hidden with B, B-C too.
        Assert.Equal(NodeVisibility.Hidden, ab.Visibility);
        Assert.Equal(NodeVisibility.Hidden, bc.Visibility);

        new SetNodeVisibilityCommand([b.Id], NodeVisibility.Visible).Execute(model);
        Assert.Equal(NodeVisibility.Dimmed, ab.Visibility);
        Assert.True(bc.IsVisible);
    }

    [Fact]
    public void Execute_ShouldMakeDescendantsFollowTheOutermostAncestor()
    {
        using var model = new ModelMgr(new StateMgr()).UseModel();
        var a = AddNode(model, "A", model.Root);
        var child = AddNode(model, "A.X", a);
        var grandchild = AddNode(model, "A.X.Y", child);
        new SetNodeVisibilityCommand([child.Id], NodeVisibility.Hidden).Execute(model);

        // Dimming the parent dims the whole subtree, the hidden child included.
        new SetNodeVisibilityCommand([a.Id], NodeVisibility.Dimmed).Execute(model);
        Assert.Equal(NodeVisibility.Dimmed, child.Visibility);
        Assert.Equal(NodeVisibility.Dimmed, grandchild.Visibility);

        // Showing the parent again brings back the child's own state for its subtree.
        new SetNodeVisibilityCommand([a.Id], NodeVisibility.Visible).Execute(model);
        Assert.Equal(NodeVisibility.Hidden, child.Visibility);
        Assert.Equal(NodeVisibility.Hidden, grandchild.Visibility);
    }

    [Fact]
    public void Revert_ShouldKeepAChildTheUserHidOnItsOwn()
    {
        using var model = new ModelMgr(new StateMgr()).UseModel();
        var a = AddNode(model, "A", model.Root);
        var child = AddNode(model, "A.X", a);
        child.SetUserVisibility(NodeVisibility.Hidden);

        var hide = new SetNodeVisibilityCommand([a.Id], NodeVisibility.Hidden);
        hide.Execute(model);
        hide.Revert(model);

        Assert.True(a.IsVisible);
        Assert.Equal(NodeVisibility.Hidden, child.Visibility);
        Assert.Equal(NodeVisibility.Hidden, child.UserVisibility);

        // Showing the child explicitly is its own step too.
        var show = new SetNodeVisibilityCommand([child.Id], NodeVisibility.Visible);
        show.Execute(model);
        Assert.True(child.IsVisible);
        show.Revert(model);
        Assert.Equal(NodeVisibility.Hidden, child.Visibility);
    }

    [Fact]
    public void Execute_ShouldSkipMissingNodes()
    {
        using var model = new ModelMgr(new StateMgr()).UseModel();
        var a = AddNode(model, "A", model.Root);

        var command = new SetNodeVisibilityCommand([a.Id, NodeId.FromName("Gone")], NodeVisibility.Dimmed);
        command.Execute(model);
        Assert.Equal(NodeVisibility.Dimmed, a.Visibility);
        command.Revert(model);
        Assert.True(a.IsVisible);
    }
}
