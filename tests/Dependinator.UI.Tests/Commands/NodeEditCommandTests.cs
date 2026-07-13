using Dependinator.Core.Parsing;
using Dependinator.UI.Modeling.Commands;
using Dependinator.UI.Modeling.Models;
using ModelNode = Dependinator.UI.Modeling.Models.Node;

namespace Dependinator.UI.Tests.Commands;

public class NodeEditCommandTests
{
    static (Model model, ModelNode node) CreateModelWithNode()
    {
        var model = new Model(() => { });
        var root = new ModelNode("", null!) { Type = NodeType.Root };
        var node = new ModelNode("Node", root);
        model.TryAddNode(node);
        return (model, node);
    }

    [Fact]
    public void Execute_ShouldSetCustomIcon_AndRevertRestorePrevious()
    {
        var (model, node) = CreateModelWithNode();
        node.CustomIconName = "Database";
        var command = new NodeEditCommand(node.Id) { IconName = "Cloud" };

        command.Execute(model);
        Assert.Equal("Cloud", node.CustomIconName);

        command.Revert(model);
        Assert.Equal("Database", node.CustomIconName);
    }

    [Fact]
    public void Execute_ShouldClearIconToDefault_WhenIconNameIsEmpty()
    {
        var (model, node) = CreateModelWithNode();
        node.CustomIconName = "Database";
        var command = new NodeEditCommand(node.Id) { IconName = "" };

        command.Execute(model);
        Assert.Null(node.CustomIconName);

        // Revert restores the previous custom icon.
        command.Revert(model);
        Assert.Equal("Database", node.CustomIconName);
    }

    [Fact]
    public void Execute_ShouldSetIcon_WhenNodeHadDefault_AndRevertRestoreNull()
    {
        var (model, node) = CreateModelWithNode();
        Assert.Null(node.CustomIconName);
        var command = new NodeEditCommand(node.Id) { IconName = "Security" };

        command.Execute(model);
        Assert.Equal("Security", node.CustomIconName);

        command.Revert(model);
        Assert.Null(node.CustomIconName);
    }

    [Fact]
    public void Execute_ShouldSetIconColor_AndRevertRestorePrevious()
    {
        var (model, node) = CreateModelWithNode();
        node.CustomIconColor = "Blue";
        var command = new NodeEditCommand(node.Id) { IconColor = "Teal" };

        command.Execute(model);
        Assert.Equal("Teal", node.CustomIconColor);

        command.Revert(model);
        Assert.Equal("Blue", node.CustomIconColor);
    }

    [Fact]
    public void Execute_ShouldClearIconColorToDefault_WhenIconColorIsEmpty()
    {
        var (model, node) = CreateModelWithNode();
        node.CustomIconColor = "Blue";
        var command = new NodeEditCommand(node.Id) { IconColor = "" };

        command.Execute(model);
        Assert.Null(node.CustomIconColor);

        // Revert restores the previous custom icon color.
        command.Revert(model);
        Assert.Equal("Blue", node.CustomIconColor);
    }

    [Fact]
    public void Execute_ShouldSetIconColor_WhenNodeHadDefault_AndRevertRestoreNull()
    {
        var (model, node) = CreateModelWithNode();
        Assert.Null(node.CustomIconColor);
        var command = new NodeEditCommand(node.Id) { IconColor = "Rose" };

        command.Execute(model);
        Assert.Equal("Rose", node.CustomIconColor);

        command.Revert(model);
        Assert.Null(node.CustomIconColor);
    }

    [Fact]
    public void Execute_ShouldSetCustomColor_AndRevertRestorePrevious()
    {
        var (model, node) = CreateModelWithNode();
        node.CustomColor = "Blue";
        var command = new NodeEditCommand(node.Id) { CustomColor = "Amber" };

        command.Execute(model);
        Assert.Equal("Amber", node.CustomColor);

        command.Revert(model);
        Assert.Equal("Blue", node.CustomColor);
    }

    [Fact]
    public void Execute_ShouldClearCustomColorToAuto_WhenCustomColorIsEmpty()
    {
        var (model, node) = CreateModelWithNode();
        node.CustomColor = "Blue";
        var command = new NodeEditCommand(node.Id) { CustomColor = "" };

        command.Execute(model);
        Assert.Null(node.CustomColor);

        // Revert restores the previous custom container color.
        command.Revert(model);
        Assert.Equal("Blue", node.CustomColor);
    }

    [Fact]
    public void Execute_ShouldNotTouchIcon_WhenIconNameIsNull()
    {
        var (model, node) = CreateModelWithNode();
        node.CustomIconName = "Database";
        var command = new NodeEditCommand(node.Id) { Boundary = new Dependinator.UI.Shared.Types.Rect(0, 0, 10, 10) };

        command.Execute(model);

        Assert.Equal("Database", node.CustomIconName);
    }
}
