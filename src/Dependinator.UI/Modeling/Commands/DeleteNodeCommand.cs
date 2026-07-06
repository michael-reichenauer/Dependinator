using Dependinator.UI.Modeling.Dtos;
using Dependinator.UI.Modeling.Models;

namespace Dependinator.UI.Modeling.Commands;

// Removes a leaf manual node; Revert restores it from a captured snapshot. Attached manual links
// are removed/restored by separate DeleteLinkCommands (composed alongside this one).
class DeleteNodeCommand(NodeId nodeId) : Command
{
    readonly NodeId nodeId = nodeId;
    NodeDto? snapshot;
    string parentName = "";

    public override void Execute(IModel model)
    {
        if (!model.Nodes.TryGetValue(nodeId, out var node))
            return;

        snapshot = node.ToDto();
        parentName = node.Parent?.Name ?? "";
        model.RemoveNode(node);
    }

    public override void Revert(IModel model)
    {
        if (snapshot is null)
            return;
        if (!model.Nodes.TryGetValue(NodeId.FromName(parentName), out var parent))
            return;

        var node = new Node(snapshot.Name, parent);
        node.SetFromDto(snapshot);
        node.UpdateStamp = model.UpdateStamp;
        model.TryAddNode(node);
        parent.AddChild(node);
    }
}
