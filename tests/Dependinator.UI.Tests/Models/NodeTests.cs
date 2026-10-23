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
    public void SetUserVisibility_ShouldPropagateParentState()
    {
        var root = new ModelNode("", null!) { Type = NodeType.Root };
        var child = new ModelNode("Child", root);
        var grandchild = new ModelNode("Grandchild", child);

        root.AddChild(child);
        child.AddChild(grandchild);

        child.SetUserVisibility(Dependinator.UI.Modeling.Models.NodeVisibility.Dimmed);

        Assert.Equal(Dependinator.UI.Modeling.Models.NodeVisibility.Dimmed, child.UserVisibility);
        Assert.Equal(Dependinator.UI.Modeling.Models.NodeVisibility.Dimmed, grandchild.ParentVisibility);
        Assert.Equal(Dependinator.UI.Modeling.Models.NodeVisibility.Dimmed, grandchild.Visibility);
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

    // The parser-supplied icon (IconName) follows the same update rules as the other parsed
    // properties and stays separate from the user's custom icon, which survives every re-parse.
    [Fact]
    public void Update_ShouldSetParsedIconName()
    {
        var node = CreateNode();

        node.Update(new Node(node.Name, new() { Type = NodeType.Resource, IconName = "Key-Vault" }));

        Assert.Equal("Key-Vault", node.IconName);
        Assert.Equal(NodeType.Resource, node.Type);
    }

    [Fact]
    public void Update_ShouldKeepIconName_WhenParsedIconIsNull()
    {
        var node = CreateNode();
        node.IconName = "Key-Vault";

        node.Update(new Node(node.Name, new() { Description = "Updated" }));

        Assert.Equal("Key-Vault", node.IconName);
    }

    [Fact]
    public void Update_ShouldClearIconName_OnNoValue()
    {
        var node = CreateNode();
        node.IconName = "Key-Vault";

        node.Update(new Node(node.Name, new() { IconName = NoValue.String }));

        Assert.Null(node.IconName);
    }

    [Fact]
    public void Update_ShouldNotTouchCustomIcon()
    {
        var node = CreateNode();
        node.CustomIconName = "Database";
        node.CustomIconColor = "Blue";

        node.Update(new Node(node.Name, new() { IconName = "Key-Vault" }));

        Assert.Equal("Database", node.CustomIconName);
        Assert.Equal("Blue", node.CustomIconColor);
        Assert.Equal("Key-Vault", node.IconName);
    }

    [Fact]
    public void ToDto_SetFromDto_ShouldRoundTripParsedAndCustomIconsSeparately()
    {
        var node = CreateNode();
        node.IconName = "Key-Vault";
        node.CustomIconName = "Database";

        var dto = node.ToDto();
        var restored = CreateNode();
        restored.SetFromDto(dto);

        Assert.Equal("Key-Vault", dto.Properties.IconName);
        Assert.Equal("Database", dto.IconName);
        Assert.Equal("Key-Vault", restored.IconName);
        Assert.Equal("Database", restored.CustomIconName);
    }

    [Fact]
    public void ToDto_ShouldOmitParsedIconName_WhenNotSet()
    {
        var json = System.Text.Json.JsonSerializer.Serialize(CreateNode().ToDto());

        Assert.DoesNotContain("IconName", json);
    }

    static ModelNode CreateNode() => new("Model.web", new ModelNode("", null!) { Type = NodeType.Root });

    [Fact]
    public void Update_ShouldNotTouchTheAlias_AndDtoShouldRoundTripIt()
    {
        var node = CreateNode();
        node.SetAlias(" Store <front> ");

        node.Update(new Node(node.Name, new() { IconName = "Key-Vault" }));

        Assert.Equal("Store <front>", node.Alias);
        Assert.Equal("Store &lt;front&gt;", node.HtmlAlias);
        Assert.Equal("Store <front>", node.DisplayName);
        Assert.Equal("web", node.ShortName); // the name from the code is untouched

        var restored = CreateNode();
        restored.SetFromDto(node.ToDto());
        Assert.Equal("Store <front>", restored.Alias);

        node.SetAlias("  ");
        Assert.Null(node.Alias);
        Assert.Equal("web", node.DisplayName);
        Assert.DoesNotContain("Alias", System.Text.Json.JsonSerializer.Serialize(node.ToDto()));
    }

    [Fact]
    public void ToDto_SetFromDto_ShouldRoundTripVisibility_AndKeepTheOldHiddenFlags()
    {
        var node = CreateNode();
        node.SetUserVisibility(Dependinator.UI.Modeling.Models.NodeVisibility.Dimmed);
        node.SetParentVisibility(Dependinator.UI.Modeling.Models.NodeVisibility.Hidden);

        var dto = node.ToDto();
        var restored = CreateNode();
        restored.SetFromDto(dto);

        // An older app reads only the flags, and keeps such a node hidden.
        Assert.True(dto.IsUserSetHidden && dto.IsParentSetHidden);
        Assert.Equal(Dependinator.UI.Modeling.Models.NodeVisibility.Dimmed, restored.UserVisibility);
        Assert.Equal(Dependinator.UI.Modeling.Models.NodeVisibility.Hidden, restored.ParentVisibility);
        Assert.DoesNotContain("Visibility", System.Text.Json.JsonSerializer.Serialize(CreateNode().ToDto()));
    }

    [Fact]
    public void SetFromDto_ShouldGiveANodeHiddenBeforeDimmingExisted_TheLegacyVisibility()
    {
        var dto = new Dependinator.UI.Modeling.Dtos.NodeDto
        {
            Name = "Model.web",
            ParentName = "",
            Type = "Type",
            Properties = new(),
            IsUserSetHidden = true,
        };
        var restored = CreateNode();

        restored.SetFromDto(dto);

        Assert.Equal(ModelNode.LegacyHiddenVisibility, restored.UserVisibility);
        Assert.Equal(Dependinator.UI.Modeling.Models.NodeVisibility.Visible, restored.ParentVisibility);
    }
}
