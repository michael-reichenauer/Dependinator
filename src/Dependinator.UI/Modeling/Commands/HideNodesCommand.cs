using Dependinator.UI.Modeling.Models;

namespace Dependinator.UI.Modeling.Commands;

// Hides or shows the given nodes as one undoable step (a selection, or a group of them).
// Execute records each node's own hidden flag and applies the new one (the node's children
// follow through Node.SetHidden), then refreshes which lines are hidden; Revert restores the
// recorded flags. A node hidden by its parent is left to the parent.
class HideNodesCommand(IReadOnlyList<NodeId> nodeIds, bool hidden) : Command
{
    readonly IReadOnlyList<NodeId> nodeIds = nodeIds;
    readonly bool hidden = hidden;
    readonly Dictionary<NodeId, bool> previous = [];

    // Two quick toggles (hide, then show again) stay two undo steps: hiding is a deliberate click,
    // not a stream of drag edits, so it never merges with the previous command.
    public override string MergeKey { get; } = Guid.NewGuid().ToString("N");

    public override void Execute(IModel model)
    {
        previous.Clear();
        foreach (var nodeId in nodeIds)
        {
            if (!model.Nodes.TryGetValue(nodeId, out var node))
                continue;
            previous[nodeId] = node.IsUserSetHidden;
            node.SetHidden(hidden, isUserSet: true);
        }
        LineVisibility.Update(model);
    }

    public override void Revert(IModel model)
    {
        foreach (var (nodeId, wasHidden) in previous)
        {
            if (model.Nodes.TryGetValue(nodeId, out var node))
                node.SetHidden(wasHidden, isUserSet: true);
        }
        LineVisibility.Update(model);
    }
}
