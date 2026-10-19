using Dependinator.Core.Parsing;
using Dependinator.UI.Modeling;
using Dependinator.UI.Modeling.Commands;
using Dependinator.UI.Modeling.Models;
using Dependinator.UI.Shared;
using Dependinator.UI.Shared.Types;
using Link = Dependinator.UI.Modeling.Models.Link;
using ModelNode = Dependinator.UI.Modeling.Models.Node;

namespace Dependinator.UI.Tests.Models;

public class ResetLayoutTests
{
    [Fact]
    public void ResetChildrenLayout_ShouldClearCustomizedFlag()
    {
        using var model = new ModelMgr(new StateMgr()).UseModel();
        var parent = AddNode(model, model.Root, "Parent", new Rect(0, 0, 1200, 400));
        var a = AddNode(model, parent, "A", new Rect(900, 900, 100, 100));
        AddNode(model, parent, "B", new Rect(900, 900, 100, 100));
        parent.IsChildrenLayoutCustomized = true;

        NodeLayout.SetDensity(NodeLayoutDensity.Balanced);
        NodeLayout.ResetChildrenLayout(parent, recursively: false);

        Assert.False(parent.IsChildrenLayoutCustomized);
        Assert.NotEqual(new Rect(900, 900, 100, 100), a.Boundary);
    }

    [Fact]
    public void ResetChildrenLayout_Recursively_ShouldClearDescendantFlags()
    {
        using var model = new ModelMgr(new StateMgr()).UseModel();
        var parent = AddNode(model, model.Root, "Parent", new Rect(0, 0, 1200, 400));
        var child = AddNode(model, parent, "Child", new Rect(0, 0, 100, 100));
        AddNode(model, child, "Child.A", new Rect(0, 0, 100, 100));
        parent.IsChildrenLayoutCustomized = true;
        child.IsChildrenLayoutCustomized = true;

        NodeLayout.SetDensity(NodeLayoutDensity.Balanced);
        NodeLayout.ResetChildrenLayout(parent, recursively: true);

        Assert.False(parent.IsChildrenLayoutCustomized);
        Assert.False(child.IsChildrenLayoutCustomized);
    }

    [Fact]
    public void ResetChildrenLayout_NotRecursively_ShouldKeepDescendantFlags()
    {
        using var model = new ModelMgr(new StateMgr()).UseModel();
        var parent = AddNode(model, model.Root, "Parent", new Rect(0, 0, 1200, 400));
        var child = AddNode(model, parent, "Child", new Rect(0, 0, 100, 100));
        AddNode(model, child, "Child.A", new Rect(0, 0, 100, 100));
        child.IsChildrenLayoutCustomized = true;

        NodeLayout.SetDensity(NodeLayoutDensity.Balanced);
        NodeLayout.ResetChildrenLayout(parent, recursively: false);

        Assert.True(child.IsChildrenLayoutCustomized);
    }

    [Fact]
    public void ResetChildrenLayout_ThenReparse_ShouldRelayoutTheContainer()
    {
        // Before the fix the reset container stayed "customized", so a re-parse skipped it
        // and only dropped new children into free slots.
        using var model = new ModelMgr(new StateMgr()).UseModel();
        var parent = AddNode(model, model.Root, "Parent", new Rect(0, 0, 1200, 400));
        AddNode(model, parent, "A", new Rect(0, 0, 100, 100));
        AddNode(model, parent, "B", new Rect(0, 0, 100, 100));
        parent.IsChildrenLayoutCustomized = true;

        NodeLayout.SetDensity(NodeLayoutDensity.Balanced);
        NodeLayout.ResetChildrenLayout(parent, recursively: false);
        ModelService.RequireChildrenLayout(model);

        Assert.True(parent.IsChildrenLayoutRequired);
    }

    [Fact]
    public void ResetChildrenLayout_ShouldClearUserWaypointsOfLinesInside()
    {
        using var model = new ModelMgr(new StateMgr()).UseModel();
        var parent = AddNode(model, model.Root, "Parent", new Rect(0, 0, 1200, 400));
        var other = AddNode(model, model.Root, "Other", new Rect(0, 0, 100, 100));
        var a = AddNode(model, parent, "A", new Rect(0, 0, 100, 100));
        var b = AddNode(model, parent, "B", new Rect(0, 0, 100, 100));
        AddLink(model, a, b);
        AddLink(model, parent, other);
        var inside = a.SourceLines.Single(line => line.Target == b);
        var outside = parent.SourceLines.Single(line => line.Target == other);
        SetUserWaypoints(inside);
        SetUserWaypoints(outside);

        NodeLayout.SetDensity(NodeLayoutDensity.Balanced);
        NodeLayout.ResetChildrenLayout(parent, recursively: false);

        Assert.False(inside.IsSegmentsUserSet);
        Assert.Empty(inside.SegmentPoints);
        Assert.True(outside.IsSegmentsUserSet);
        Assert.NotEmpty(outside.SegmentPoints);
    }

    [Fact]
    public void ResetLayoutCommand_Revert_ShouldRestoreGeometryFlagsAndWaypoints()
    {
        using var model = new ModelMgr(new StateMgr()).UseModel();
        var parent = AddNode(model, model.Root, "Parent", new Rect(0, 0, 1200, 400));
        var a = AddNode(model, parent, "A", new Rect(900, 900, 160, 120));
        var b = AddNode(model, parent, "B", new Rect(300, 40, 100, 100));
        AddLink(model, a, b);
        var line = a.SourceLines.Single(l => l.Target == b);
        SetUserWaypoints(line);
        parent.IsChildrenLayoutCustomized = true;
        parent.ContainerZoom = 0.3;
        parent.ContainerOffset = new Pos(11, 22);

        NodeLayout.SetDensity(NodeLayoutDensity.Balanced);
        var command = new ResetLayoutCommand(parent.Id, recursively: false);
        command.Execute(model);
        Assert.NotEqual(new Rect(900, 900, 160, 120), a.Boundary);

        command.Revert(model);

        Assert.Equal(new Rect(900, 900, 160, 120), a.Boundary);
        Assert.Equal(new Rect(300, 40, 100, 100), b.Boundary);
        Assert.True(parent.IsChildrenLayoutCustomized);
        Assert.Equal(0.3, parent.ContainerZoom);
        Assert.Equal(new Pos(11, 22), parent.ContainerOffset);
        Assert.True(line.IsSegmentsUserSet);
        Assert.Equal([new Pos(10, 20)], line.SegmentPoints);
    }

    [Fact]
    public void ResetLayoutCommand_Redo_ShouldRestoreTheResetLayout()
    {
        using var model = new ModelMgr(new StateMgr()).UseModel();
        var parent = AddNode(model, model.Root, "Parent", new Rect(0, 0, 1200, 400));
        var a = AddNode(model, parent, "A", new Rect(900, 900, 100, 100));
        AddNode(model, parent, "B", new Rect(300, 40, 100, 100));
        parent.IsChildrenLayoutCustomized = true;

        NodeLayout.SetDensity(NodeLayoutDensity.Balanced);
        var command = new ResetLayoutCommand(parent.Id, recursively: false);
        command.Execute(model);
        var resetBoundary = a.Boundary;
        var resetZoom = parent.ContainerZoom;

        command.Revert(model);
        command.Execute(model);

        Assert.Equal(resetBoundary, a.Boundary);
        Assert.Equal(resetZoom, parent.ContainerZoom);
        Assert.False(parent.IsChildrenLayoutCustomized);
    }

    static void SetUserWaypoints(Line line)
    {
        line.SetSegmentPoints([new Pos(10, 20)]);
        line.IsSegmentsUserSet = true;
    }

    static ModelNode AddNode(IModel model, ModelNode parent, string name, Rect boundary)
    {
        var node = new ModelNode(name, parent) { Type = NodeType.Namespace, Boundary = boundary };
        parent.AddChild(node);
        model.TryAddNode(node);
        return node;
    }

    static void AddLink(IModel model, ModelNode source, ModelNode target)
    {
        var link = new Link(source, target);
        new LineService().AddLinesFromSourceToTarget(model, link);
    }
}
