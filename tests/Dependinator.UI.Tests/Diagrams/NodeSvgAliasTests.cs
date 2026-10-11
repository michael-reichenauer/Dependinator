using Dependinator.Core.Parsing;
using Dependinator.UI.Diagrams.Svg;
using Dependinator.UI.Shared.Types;
using ModelNode = Dependinator.UI.Modeling.Models.Node;

namespace Dependinator.UI.Tests.Diagrams;

// A display alias replaces the drawn label; the tooltip keeps the real long name first (the
// identity, which the e2e locators match) and adds the alias after it.
public class NodeSvgAliasTests
{
    static ModelNode CreateNode()
    {
        var root = new ModelNode("", null!) { Type = NodeType.Root };
        var parent = new ModelNode("Demo", root) { Type = NodeType.Assembly };
        root.AddChild(parent);
        var node = new ModelNode("Demo.frontend", parent)
        {
            Type = NodeType.ClassType,
            Boundary = new Rect(0, 0, 80, 40),
        };
        parent.AddChild(node);
        return node;
    }

    [Fact]
    public void GetNodeIconSvg_ShouldShowTheAlias_AndKeepTheLongNameInTheTitle()
    {
        var node = CreateNode();
        node.SetAlias("Storefront");

        var svg = NodeSvg.GetNodeIconSvg(node, new Rect(0, 0, 80, 40), 1.0, isEditing: false);

        Assert.Contains(">Storefront</text>", svg);
        Assert.DoesNotContain(">frontend</text>", svg);
        Assert.Contains("<title>frontend (Storefront)</title>", svg);
    }

    [Fact]
    public void GetNodeContainerSvg_ShouldShowTheAlias_AndTheMemberRowToo()
    {
        var node = CreateNode();
        node.SetAlias("Storefront");

        var container = NodeSvg.GetNodeContainerSvg(node, new Rect(0, 0, 80, 40), 2.0, "");
        Assert.Contains(">Storefront</text>", container);
        Assert.Contains("<title>frontend (Storefront)", container);

        node.Type = NodeType.MethodMember;
        var member = NodeSvg.GetMemberNodeSvg(node, new Rect(0, 0, 80, 18), 2.0, isEditing: false);
        Assert.Contains(">Storefront</text>", member);
    }

    [Fact]
    public void GetNodeIconSvg_ShouldBeUnchanged_WithoutAnAlias()
    {
        var node = CreateNode();

        var svg = NodeSvg.GetNodeIconSvg(node, new Rect(0, 0, 80, 40), 1.0, isEditing: false);

        Assert.Contains(">frontend</text>", svg);
        Assert.Contains("<title>frontend</title>", svg);
    }
}
