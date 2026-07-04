using Dependinator.Core.Parsing;
using Dependinator.UI.Shared.Types;
using ModelNode = Dependinator.UI.Modeling.Models.Node;

namespace Dependinator.UI.Tests.Models;

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
    public void Boundary_ShouldCoverParentViewportWhenPassThrough()
    {
        var root = new ModelNode("", null!) { Type = NodeType.Root };
        var assembly = new ModelNode("Assembly", root)
        {
            Boundary = new Rect(100, 100, 200, 160),
            ContainerZoom = 0.5,
            ContainerOffset = new Pos(10, 20),
        };
        var ns = new ModelNode("Assembly.Ns", assembly) { IsPassThrough = true };
        root.AddChild(assembly);
        assembly.AddChild(ns);

        // The parent's viewport expressed in its inner coordinate space
        Assert.Equal(new Rect(-20, -40, 400, 320), ns.Boundary);
    }

    [Fact]
    public void Boundary_ShouldCoverParentViewportForPassThroughChain()
    {
        var root = new ModelNode("", null!) { Type = NodeType.Root };
        var assembly = new ModelNode("Assembly", root)
        {
            Boundary = new Rect(0, 0, 200, 160),
            ContainerZoom = 0.5,
            ContainerOffset = new Pos(0, 0),
        };
        var outerNs = new ModelNode("Assembly.Outer", assembly) { IsPassThrough = true, ContainerZoom = 0.25 };
        var innerNs = new ModelNode("Assembly.Outer.Inner", outerNs) { IsPassThrough = true };
        root.AddChild(assembly);
        assembly.AddChild(outerNs);
        outerNs.AddChild(innerNs);

        Assert.Equal(new Rect(0, 0, 400, 320), outerNs.Boundary);
        Assert.Equal(new Rect(0, 0, 1600, 1280), innerNs.Boundary);
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
}
