using Dependinator.UI.Modeling.Models;
using Dependinator.UI.Shared.Types;

namespace Dependinator.UI.Modeling.Commands;

// Resets the automatic layout of a node's children (or of all its descendants) as one undoable
// step. The first Execute records the layout state of the node's subtree, runs the reset and
// records the result; Revert restores the first record and a redo the second, so a redo shows
// exactly the layout the user saw instead of laying out again.
class ResetLayoutCommand(NodeId nodeId, bool recursively) : Command
{
    readonly NodeId nodeId = nodeId;
    readonly bool recursively = recursively;
    LayoutState? before;
    LayoutState? after;

    // A reset is a deliberate menu action, never merged with the previous command.
    public override string MergeKey { get; } = Guid.NewGuid().ToString("N");

    public override void Execute(IModel model)
    {
        if (after is not null)
        {
            after.Restore(model);
            return;
        }

        if (!model.Nodes.TryGetValue(nodeId, out var node))
            return;

        before = LayoutState.Capture(node);
        NodeLayout.ResetChildrenLayout(node, recursively);
        after = LayoutState.Capture(node);
    }

    public override void Revert(IModel model) => before?.Restore(model);

    // Everything a reset may change in the subtree: each node's boundary (sizes too), container
    // transform and layout flags, and the segment points of the lines the subtree draws. A
    // pass-through node's boundary is derived from its parent, so only its transform is kept.
    sealed class LayoutState(Dictionary<NodeId, NodeState> nodes, Dictionary<LineId, LineState> lines)
    {
        public static LayoutState Capture(Node root)
        {
            var nodes = new Dictionary<NodeId, NodeState>();
            var lines = new Dictionary<LineId, LineState>();
            foreach (var node in root.DescendantsAndSelfPreOrder())
            {
                nodes[node.Id] = new NodeState(
                    node.IsPassThrough ? null : node.Boundary,
                    node.ContainerZoom,
                    node.ContainerOffset,
                    node.IsChildrenLayoutCustomized,
                    node.IsChildrenLayoutRequired
                );
                foreach (var line in node.SourceLines.Concat(node.DirectLines))
                    lines[line.Id] = new LineState([.. line.SegmentPoints], line.IsSegmentsUserSet);
            }
            return new LayoutState(nodes, lines);
        }

        public void Restore(IModel model)
        {
            foreach (var (id, state) in nodes)
            {
                if (!model.Nodes.TryGetValue(id, out var node))
                    continue;
                if (state.Boundary is { } boundary)
                    node.Boundary = boundary;
                node.ContainerZoom = state.ContainerZoom;
                node.ContainerOffset = state.ContainerOffset;
                node.IsChildrenLayoutCustomized = state.IsChildrenLayoutCustomized;
                node.IsChildrenLayoutRequired = state.IsChildrenLayoutRequired;
            }

            foreach (var (id, state) in lines)
            {
                if (!model.Lines.TryGetValue(id, out var line))
                    continue;
                line.SetSegmentPoints(state.SegmentPoints);
                line.IsSegmentsUserSet = state.IsSegmentsUserSet;
            }
        }
    }

    readonly record struct NodeState(
        Rect? Boundary,
        double ContainerZoom,
        Pos ContainerOffset,
        bool IsChildrenLayoutCustomized,
        bool IsChildrenLayoutRequired
    );

    readonly record struct LineState(IReadOnlyList<Pos> SegmentPoints, bool IsSegmentsUserSet);
}
