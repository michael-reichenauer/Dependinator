using Dependinator.UI.Modeling.Commands;
using Dependinator.UI.Modeling.Models;

namespace Dependinator.UI.Diagrams;

// A node the user dimmed or hid, as the hidden nodes panel lists it. IsParentNotVisible: it sits
// inside a dimmed or hidden node, follows that one and cannot be changed on its own.
record HiddenNode(
    NodeId Id,
    string Name,
    NodeId ParentId,
    string ParentName,
    NodeVisibility Visibility,
    bool IsParentNotVisible
);

// The nodes the user dimmed or hid themselves (descendants that only follow them are not listed),
// for the hidden nodes panel, its count in the app bar and the badges on their parents; changes
// are undoable steps.
interface IHiddenNodeService
{
    // Sorted by full name, so the nodes of one part of the model sit together.
    IReadOnlyList<HiddenNode> GetNodes();
    int Count { get; }

    // How many of each node's children the user hid (dimmed ones are still drawn), for the badge
    // on that node. The same instance while unchanged, so a renderer can tell by reference that
    // its badges are current.
    IReadOnlyDictionary<NodeId, int> GetHiddenChildCounts();

    void SetVisibility(NodeId nodeId, NodeVisibility visibility);

    // Shows the given listed nodes again as one undo step.
    void Show(IReadOnlyList<NodeId> nodeIds);

    // Opens the panel (listing only the hidden children of parentId when given).
    void OpenPanel(NodeId? parentId = null);

    // The parent the open panel lists the hidden children of, with its name; null for the whole
    // model. A parent that is gone, or from another model, ends the scope.
    (NodeId Id, string Name)? GetPanelScope();

    // Closes the panel, or opens it on the whole model (also when it lists one parent's).
    void TogglePanel();
}

[Scoped]
class HiddenNodeService(
    IModelMgr modelMgr,
    ICommandService commandService,
    IViewOptions viewOptions,
    IPathFinderService pathFinderService,
    IApplicationEvents applicationEvents
) : IHiddenNodeService
{
    (int Structure, int Nodes, int Visibility) cachedFor = (-1, -1, -1);
    IReadOnlyList<HiddenNode> nodes = [];
    IReadOnlyDictionary<NodeId, int> hiddenChildCounts = new Dictionary<NodeId, int>();
    string scopeModelPath = ""; // Node ids are names, so the same id can exist in another model

    public int Count => GetNodes().Count;

    public IReadOnlyList<HiddenNode> GetNodes()
    {
        Update();
        return nodes;
    }

    public IReadOnlyDictionary<NodeId, int> GetHiddenChildCounts()
    {
        Update();
        return hiddenChildCounts;
    }

    void Update() =>
        modelMgr.WithModel(m =>
        {
            var key = (m.StructureVersion, m.Nodes.Count, m.VisibilityVersion);
            if (key == cachedFor)
                return;

            nodes = m
                .Nodes.Values.Where(n => n.UserVisibility != NodeVisibility.Visible && !n.IsRoot)
                .OrderBy(n => n.LongName, StringComparer.Ordinal)
                .Select(n => new HiddenNode(
                    n.Id,
                    n.DisplayName,
                    n.Parent.Id,
                    n.Parent.IsRoot ? "" : n.Parent.LongName,
                    n.UserVisibility,
                    !n.Parent.IsVisible
                ))
                .ToList();
            var counts = nodes
                .Where(n => n.Visibility == NodeVisibility.Hidden)
                .GroupBy(n => n.ParentId)
                .ToDictionary(g => g.Key, g => g.Count());
            var isSame =
                counts.Count == hiddenChildCounts.Count
                && counts.All(c => hiddenChildCounts.TryGetValue(c.Key, out var count) && count == c.Value);
            if (!isSame)
                hiddenChildCounts = counts;
            cachedFor = key;
        });

    // The option already on is a no-op, not an undo step.
    public void SetVisibility(NodeId nodeId, NodeVisibility visibility)
    {
        var current = modelMgr.WithModel(m => m.Nodes.TryGetValue(nodeId, out var n) ? n.UserVisibility : visibility);
        if (current != visibility)
            commandService.Do(new SetNodeVisibilityCommand([nodeId], visibility));
    }

    public void Show(IReadOnlyList<NodeId> nodeIds)
    {
        if (nodeIds.Count > 0)
            commandService.Do(new SetNodeVisibilityCommand(nodeIds, NodeVisibility.Visible));
    }

    // The panel shares the top-right corner with the analysis panels, so it closes them; their
    // highlighted lines are baked into the tiles.
    public void OpenPanel(NodeId? parentId = null)
    {
        if (viewOptions.IsCyclesShown || viewOptions.IsRulesShown)
        {
            viewOptions.SetCyclesShown(false);
            viewOptions.SetRulesShown(false);
            applicationEvents.TriggerModelChanged();
        }
        pathFinderService.Close();
        scopeModelPath = modelMgr.WithModel(m => m.Path);
        viewOptions.SetHiddenNodesShown(true, parentId);
        applicationEvents.TriggerUIStateChanged();
    }

    public void TogglePanel()
    {
        if (viewOptions.IsHiddenNodesShown && GetPanelScope() is null)
        {
            viewOptions.SetHiddenNodesShown(false);
            applicationEvents.TriggerUIStateChanged();
            return;
        }
        OpenPanel();
    }

    public (NodeId Id, string Name)? GetPanelScope()
    {
        if (viewOptions.HiddenNodesParentId is not { } parentId)
            return null;
        var name = modelMgr.WithModel(m =>
            m.Path == scopeModelPath && m.Nodes.TryGetValue(parentId, out var node) ? node.LongName : null
        );
        if (name is null)
        {
            viewOptions.SetHiddenNodesShown(viewOptions.IsHiddenNodesShown);
            return null;
        }
        return (parentId, name);
    }
}
