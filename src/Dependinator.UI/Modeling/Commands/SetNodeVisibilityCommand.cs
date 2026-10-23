using Dependinator.UI.Modeling.Models;

namespace Dependinator.UI.Modeling.Commands;

// Dims, hides or shows the given nodes as one undoable step (a selection, or a group of them).
// Execute records each node's own state and applies the new one (the descendants follow through
// Node.SetUserVisibility), then refreshes the lines; Revert restores the recorded states.
class SetNodeVisibilityCommand(IReadOnlyList<NodeId> nodeIds, NodeVisibility visibility) : Command
{
    readonly IReadOnlyList<NodeId> nodeIds = nodeIds;
    readonly NodeVisibility visibility = visibility;
    readonly Dictionary<NodeId, NodeVisibility> previous = [];

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
            previous[nodeId] = node.UserVisibility;
            node.SetUserVisibility(visibility);
        }
        LineVisibility.Update(model);
        model.BumpVisibilityVersion();
    }

    public override void Revert(IModel model)
    {
        foreach (var (nodeId, wasVisibility) in previous)
        {
            if (model.Nodes.TryGetValue(nodeId, out var node))
                node.SetUserVisibility(wasVisibility);
        }
        LineVisibility.Update(model);
        model.BumpVisibilityVersion();
    }
}
