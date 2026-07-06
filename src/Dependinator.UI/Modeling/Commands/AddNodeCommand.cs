using Dependinator.UI.Modeling.Models;
using Dependinator.UI.Shared.Types;

namespace Dependinator.UI.Modeling.Commands;

// Adds a manually created (user-drawn) node under an existing parent. Revert removes it.
class AddNodeCommand(string name, string parentName, Rect boundary) : Command
{
    readonly string name = name;
    readonly string parentName = parentName;
    readonly Rect boundary = boundary;

    public override void Execute(IModel model)
    {
        if (model.Nodes.ContainsKey(NodeId.FromName(name)))
            return;
        if (!model.Nodes.TryGetValue(NodeId.FromName(parentName), out var parent))
            return;

        var node = new Node(name, parent)
        {
            IsManual = true,
            Boundary = boundary,
            UpdateStamp = model.UpdateStamp,
        };
        model.TryAddNode(node);
        parent.AddChild(node);
        // Keep the user's chosen position; don't let auto-layout rearrange the parent's children.
        parent.IsChildrenLayoutCustomized = true;
    }

    public override void Revert(IModel model)
    {
        if (model.Nodes.TryGetValue(NodeId.FromName(name), out var node))
            model.RemoveNode(node);
    }
}
