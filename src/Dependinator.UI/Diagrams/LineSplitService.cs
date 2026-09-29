using Dependinator.UI.Diagrams.Dependencies;
using Dependinator.UI.Modeling;
using Dependinator.UI.Modeling.Models;

namespace Dependinator.UI.Diagrams;

// User control over on-demand direct lines: a node's direct-line depth splits its aggregated
// line bundles that many levels into its subtree, for its incoming as well as its outgoing
// links (see RepLineService). The state is transient view state, like hiding a node: not
// saved, not undoable. The dependency explorer's lines are the other kind of on-demand lines
// (DependenciesService); ClearAll switches those off too.
interface ILineSplitService
{
    int GetDepth(NodeId nodeId);
    bool HasSplits(NodeId nodeId);
    void IncreaseDepth(NodeId nodeId);
    void DecreaseDepth(NodeId nodeId);
    void Clear(NodeId nodeId);
    void ClearAll();
}

[Scoped]
class LineSplitService(
    IModelMgr modelMgr,
    IApplicationEvents applicationEvents,
    IDependenciesService dependenciesService
) : ILineSplitService
{
    public int GetDepth(NodeId nodeId)
    {
        using var model = modelMgr.UseModel();
        return model.Nodes.TryGetValue(nodeId, out var node) ? node.LineSplitDepth : 0;
    }

    public bool HasSplits(NodeId nodeId) => GetDepth(nodeId) > 0;

    // One level per click, capped at the subtree height: a deeper value would change nothing
    // on screen, and the badge should not count past what can be shown.
    public void IncreaseDepth(NodeId nodeId) =>
        Update(nodeId, node => Math.Min(node.LineSplitDepth + 1, GetHeight(node)));

    public void DecreaseDepth(NodeId nodeId) => Update(nodeId, node => Math.Max(node.LineSplitDepth - 1, 0));

    // Clears the node and its whole subtree, so one action restores the aggregated bundle even
    // when deeper nodes were split individually.
    public void Clear(NodeId nodeId)
    {
        var isChanged = false;
        using (var model = modelMgr.UseModel())
        {
            if (!model.Nodes.TryGetValue(nodeId, out var node))
                return;
            isChanged = ClearDepths(node.DescendantsAndSelfPreOrder());
            if (isChanged)
                model.BumpStructureVersion();
        }

        if (isChanged)
            NotifyChanged();
    }

    public void ClearAll()
    {
        var isChanged = false;
        using (var model = modelMgr.UseModel())
        {
            isChanged = ClearDepths(model.Nodes.Values);
            if (isChanged)
                model.BumpStructureVersion();
        }

        if (isChanged)
            NotifyChanged();

        dependenciesService.SetShowLines(false);
    }

    void Update(NodeId nodeId, Func<Node, int> getDepth)
    {
        using (var model = modelMgr.UseModel())
        {
            if (!model.Nodes.TryGetValue(nodeId, out var node))
                return;
            var depth = getDepth(node);
            if (depth == node.LineSplitDepth)
                return;
            node.LineSplitDepth = depth;
            model.BumpStructureVersion();
        }

        NotifyChanged();
    }

    static bool ClearDepths(IEnumerable<Node> nodes)
    {
        var isChanged = false;
        foreach (var node in nodes)
        {
            if (node.LineSplitDepth == 0)
                continue;
            node.LineSplitDepth = 0;
            isChanged = true;
        }
        return isChanged;
    }

    // The split state changes which lines exist and which render, so the tiles are rebuilt
    // (ModelChanged clears the tile cache) and the toolbars re-read the depth.
    void NotifyChanged()
    {
        applicationEvents.TriggerModelChanged();
        applicationEvents.TriggerUIStateChanged();
    }

    // Levels below the node, not counting pass-through nodes (RepLineService never counts them
    // as a level either).
    static int GetHeight(Node node)
    {
        var height = 0;
        foreach (var child in node.Children)
        {
            height = Math.Max(height, GetHeight(child) + (child.IsPassThrough ? 0 : 1));
        }
        return height;
    }
}
