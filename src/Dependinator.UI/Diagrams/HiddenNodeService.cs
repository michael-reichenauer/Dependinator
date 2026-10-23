using Dependinator.UI.Modeling.Commands;
using Dependinator.UI.Modeling.Models;

namespace Dependinator.UI.Diagrams;

// A node the user dimmed or hid, as the hidden nodes panel lists it. IsParentNotVisible: it sits
// inside a dimmed or hidden node, follows that one and cannot be changed on its own.
record HiddenNode(NodeId Id, string Name, string ParentName, NodeVisibility Visibility, bool IsParentNotVisible);

// The nodes the user dimmed or hid themselves (descendants that only follow them are not listed),
// for the hidden nodes panel and its count in the app bar; changes are undoable steps.
interface IHiddenNodeService
{
    // Sorted by full name, so the nodes of one part of the model sit together.
    IReadOnlyList<HiddenNode> GetNodes();
    int Count { get; }
    void SetVisibility(NodeId nodeId, NodeVisibility visibility);

    // Shows every listed node again as one undo step.
    void ShowAll();
}

[Scoped]
class HiddenNodeService(IModelMgr modelMgr, ICommandService commandService) : IHiddenNodeService
{
    (int Structure, int Nodes, int Visibility) cachedFor = (-1, -1, -1);
    IReadOnlyList<HiddenNode> nodes = [];

    public int Count => GetNodes().Count;

    public IReadOnlyList<HiddenNode> GetNodes()
    {
        var key = modelMgr.WithModel(m => (m.StructureVersion, m.Nodes.Count, m.VisibilityVersion));
        if (key == cachedFor)
            return nodes;

        nodes = modelMgr.WithModel(m =>
            m.Nodes.Values.Where(n => n.UserVisibility != NodeVisibility.Visible && !n.IsRoot)
                .OrderBy(n => n.LongName, StringComparer.Ordinal)
                .Select(n => new HiddenNode(
                    n.Id,
                    n.DisplayName,
                    n.Parent.IsRoot ? "" : n.Parent.LongName,
                    n.UserVisibility,
                    !n.Parent.IsVisible
                ))
                .ToList()
        );
        cachedFor = key;
        return nodes;
    }

    public void SetVisibility(NodeId nodeId, NodeVisibility visibility) =>
        commandService.Do(new SetNodeVisibilityCommand([nodeId], visibility));

    public void ShowAll()
    {
        var ids = GetNodes().Select(n => n.Id).ToList();
        if (ids.Count > 0)
            commandService.Do(new SetNodeVisibilityCommand(ids, NodeVisibility.Visible));
    }
}
