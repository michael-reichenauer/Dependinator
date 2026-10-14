using Dependinator.Core.Parsing;
using Dependinator.UI.Modeling;
using Dependinator.UI.Modeling.Commands;
using Dependinator.UI.Modeling.Models;
using Dependinator.UI.Shared;
using Dependinator.UI.Shared.Types;
using Link = Dependinator.UI.Modeling.Models.Link;
using Node = Dependinator.UI.Modeling.Models.Node;

namespace Dependinator.UI.Tests.Commands;

// Hiding is an undoable edit: the nodes' own hidden flags are restored on revert, children follow
// their parent, a child the user hid on its own stays hidden when the parent is shown again, and
// the lines to hidden nodes follow.
public class HideNodesCommandTests
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

        var command = new HideNodesCommand([a.Id, b.Id], hidden: true);
        command.Execute(model);

        Assert.True(a.IsUserSetHidden && b.IsUserSetHidden);
        Assert.True(child.IsHidden && child.IsParentSetHidden && !child.IsUserSetHidden);
        Assert.True(line.IsHidden);
        Assert.True(otherLine.IsHidden); // one end hidden
        Assert.False(c.IsHidden);

        command.Revert(model);

        Assert.False(a.IsHidden || b.IsHidden || child.IsHidden);
        Assert.False(line.IsHidden || otherLine.IsHidden);
    }

    [Fact]
    public void Revert_ShouldKeepAChildTheUserHidOnItsOwn()
    {
        using var model = new ModelMgr(new StateMgr()).UseModel();
        var a = AddNode(model, "A", model.Root);
        var child = AddNode(model, "A.X", a);
        child.SetHidden(true, isUserSet: true);

        var hide = new HideNodesCommand([a.Id], hidden: true);
        hide.Execute(model);
        hide.Revert(model);

        Assert.False(a.IsHidden);
        Assert.True(child.IsHidden && child.IsUserSetHidden);

        // Showing the parent explicitly (the toggle on a hidden node) is its own step too.
        var show = new HideNodesCommand([child.Id], hidden: false);
        show.Execute(model);
        Assert.False(child.IsHidden);
        show.Revert(model);
        Assert.True(child.IsHidden);
    }

    [Fact]
    public void Execute_ShouldSkipMissingNodes()
    {
        using var model = new ModelMgr(new StateMgr()).UseModel();
        var a = AddNode(model, "A", model.Root);

        var command = new HideNodesCommand([a.Id, NodeId.FromName("Gone")], hidden: true);
        command.Execute(model);
        Assert.True(a.IsHidden);
        command.Revert(model);
        Assert.False(a.IsHidden);
    }
}
