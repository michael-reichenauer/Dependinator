using Dependinator.Models;
using Dependinator.Parsing;
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

        Assert.Equal(new Pos(10*2 + 5*2, 20*2 + 6*2), pos);
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
}
