using Dependinator.Models;
using DependinatorCore.Parsing;
using ModelNode = Dependinator.Models.Node;

namespace Dependinator.Tests.Models;

public class NodeTests
{
    [Fact]
    public void GetPosAndZoom_ShouldApplyParentTransform()
    {
        var root = new ModelNode("", null!)
        {
            Type = NodeType.Root,
            ContainerZoom = 2,
            ContainerOffset = new Pos(10, 20),
        };
        var child = new ModelNode("Child", root) { Boundary = new Rect(5, 6, 10, 12) };
        root.AddChild(child);

        var (pos, zoom) = child.GetPosAndZoom();
        var (centerPos, centerZoom) = child.GetCenterPosAndZoom();

        Assert.Equal(new Pos(20, 32), pos);
        Assert.Equal(2, zoom);
        Assert.Equal(new Pos(30, 44), centerPos);
        Assert.Equal(2, centerZoom);
    }

    [Fact]
    public void SetHidden_ShouldPropagateParentState()
    {
        var root = new ModelNode("", null!) { Type = NodeType.Root };
        var child = new ModelNode("Child", root);
        var grandchild = new ModelNode("Grandchild", child);

        root.AddChild(child);
        child.AddChild(grandchild);

        child.SetHidden(true, isUserSet: true);

        Assert.True(child.IsUserSetHidden);
        Assert.True(grandchild.IsParentSetHidden);
        Assert.True(grandchild.IsHidden);
    }

    [Fact]
    public void GetTotalBounds_ShouldIncludeAllChildren()
    {
        var root = new ModelNode("", null!) { Type = NodeType.Root };
        var child1 = new ModelNode("Child1", root) { Boundary = new Rect(0, 0, 10, 10) };
        var child2 = new ModelNode("Child2", root) { Boundary = new Rect(10, 5, 5, 5) };

        root.AddChild(child1);
        root.AddChild(child2);

        var bounds = root.GetTotalBounds();

        Assert.Equal(new Rect(0, 0, 15, 10), bounds);
    }

    [Fact]
    public void EnsureLayoutForPath_ShouldStabilizeCenterPosAndZoomForDeferredLayout()
    {
        var root = new ModelNode("", null!)
        {
            Type = NodeType.Root,
            Boundary = new Rect(0, 0, 1000, 1000),
            ContainerZoom = 1,
        };
        var parent = new ModelNode("Parent", root) { Type = NodeType.Type, Boundary = new Rect(100, 100, 100, 100) };
        root.AddChild(parent);

        var child = new ModelNode("Child", parent) { Type = NodeType.Type, Boundary = Rect.None };
        parent.AddChild(child);

        var (beforePos, beforeZoom) = child.GetCenterPosAndZoom();
        var didLayout = child.EnsureLayoutForPath();
        var (afterPos, afterZoom) = child.GetCenterPosAndZoom();

        Assert.True(didLayout);
        Assert.False(parent.IsChildrenLayoutRequired);
        Assert.NotEqual(beforePos, afterPos);
        Assert.NotEqual(beforeZoom, afterZoom);
    }

    [Fact]
    public void EnsureLayoutForPath_ShouldReturnFalseWhenNoAncestorNeedsLayout()
    {
        var root = new ModelNode("", null!)
        {
            Type = NodeType.Root,
            Boundary = new Rect(0, 0, 1000, 1000),
            ContainerZoom = 1,
        };
        var child = new ModelNode("Child", root) { Boundary = new Rect(100, 100, 100, 100) };
        root.AddChild(child);

        var didLayout = child.EnsureLayoutForPath();

        Assert.False(didLayout);
    }
}
