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
    public void Execute_ShouldNotTouchIcon_WhenIconNameIsNull()
    {
        var (model, node) = CreateModelWithNode();
        node.CustomIconName = "Database";
        var command = new NodeEditCommand(node.Id) { Boundary = new Dependinator.UI.Shared.Types.Rect(0, 0, 10, 10) };

        command.Execute(model);

        Assert.Equal("Database", node.CustomIconName);
    }
}
