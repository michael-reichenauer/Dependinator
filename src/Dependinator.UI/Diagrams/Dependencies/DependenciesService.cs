using Dependinator.UI.Diagrams.Interaction;
using Dependinator.UI.Modeling;
using Dependinator.UI.Modeling.Models;

// The dependency explorer tree that shows a selected node's references and dependencies
// alongside the diagram.
namespace Dependinator.UI.Diagrams.Dependencies;

enum TreeType
{
    References,
    Dependencies,
}

interface IDependenciesService
{
    bool IsShowExplorer { get; }

    TreeType TreeType { get; }
    string Title { get; }
    string Subtitle { get; }
    IReadOnlyList<TreeItem> TreeItems { get; }

    Task ShowNodeAsync(NodeId nodeId);
    void ToggleExpandAll(TreeItem treeItem);
    Task ShowEditorAsync(NodeId nodeId);
    void ShowDirectLine(NodeId nodeId);
    bool TryGetLine(LineId lineId, out Line line);
    void HideDirectLine(LineId lineId);
    void ShowReferences();
    void ShowDependencies();
    void Close();
    void Clicked(PointerId pointerId);
}

[Scoped]
class DependenciesService(
    ISelectionService selectionService,
    IApplicationEvents applicationEvents,
    IModelMgr modelMgr,
    INavigationService navigationService
) : IDependenciesService
{
    string selectedId = "";
    TreeType treeType = TreeType.References;

    public IReadOnlyList<TreeItem> TreeItems { get; private set; } = [];
    public TreeType TreeType => treeType;
    public string Title { get; private set; } = "";
    public string Subtitle { get; private set; } = "";
    public bool IsShowExplorer { get; private set; }

    public void ShowReferences() => Show(TreeType.References);

    public void ShowDependencies() => Show(TreeType.Dependencies);

    public void Clicked(PointerId pointerId)
    {
        if (IsShowExplorer && pointerId.Id != selectedId)
        {
            Close();
        }
    }

    public void ShowDirectLine(NodeId otherNodeId)
    {
        if (!selectionService.SelectedId.IsNode)
            return;

        var thisNodeId = NodeId.FromId(selectionService.SelectedId.Id);
        if (thisNodeId == otherNodeId)
            return;

        var (sourceId, targetId) =
            treeType is TreeType.Dependencies ? (thisNodeId, otherNodeId) : (otherNodeId, thisNodeId);

        using var model = modelMgr.UseModel();

        if (!model.Nodes.TryGetValue(sourceId, out var sourceNode))
            return;
        if (!model.Nodes.TryGetValue(targetId, out var targetNode))
            return;

        var directLineId = LineId.FromDirect(sourceNode.Name, targetNode.Name);
        if (model.Lines.TryGetValue(directLineId, out var existingLine))
            return;

        var ancestor = sourceNode.LowestCommonAncestor(targetNode);
        var directLine = new Line(sourceNode, targetNode, isDirect: true, id: directLineId)
        {
            RenderAncestor = ancestor,
            IsHidden = false,
        };

        ancestor.AddDirectLine(directLine);
        model.TryAddLine(directLine);

        applicationEvents.TriggerModelChanged();
        applicationEvents.TriggerUIStateChanged();
    }

    public bool TryGetLine(LineId lineId, out Line line)
    {
        using var model = modelMgr.UseModel();
        return model.Lines.TryGetValue(lineId, out line!);
    }

    public void HideDirectLine(LineId lineId)
    {
        var shouldUnselect = selectionService.SelectedId.IsLine && selectionService.SelectedId.Id == lineId.Value;

        using var model = modelMgr.UseModel();

        if (!model.Lines.TryGetValue(lineId, out var line))
            return;

        model.RemoveLine(line);

        applicationEvents.TriggerModelChanged();

        if (shouldUnselect)
            selectionService.Unselect();
        applicationEvents.TriggerUIStateChanged();
    }

    public async Task ShowNodeAsync(NodeId nodeId)
    {
        await navigationService.ShowNodeAsync(nodeId);
    }

    public async Task ShowEditorAsync(NodeId nodeId)
    {
        await navigationService.ShowEditor(nodeId);
    }

    public void ToggleExpandAll(TreeItem treeItem)
    {
        bool shouldExpand = treeItem.GetThisAndDescendants().Any(ti => !ti.Expanded);
        SetExpandedAll(treeItem, shouldExpand);

        applicationEvents.TriggerUIStateChanged();
    }

    // Expands the item before recursing, since expanding an item creates its lazy children,
    // which must happen before they can be visited.
    static void SetExpandedAll(TreeItem treeItem, bool expanded)
    {
        treeItem.Expanded = expanded;
        foreach (var child in (treeItem.Children ?? []).Cast<TreeItem>())
        {
            SetExpandedAll(child, expanded);
        }
    }

    public void Close()
    {
        IsShowExplorer = false;
        selectedId = "";
        applicationEvents.TriggerUIStateChanged();
    }

    void Show(TreeType type)
    {
        treeType = type;
        TreeItems = GetTreeItems(type);

        IsShowExplorer = true;
        applicationEvents.TriggerUIStateChanged();
    }

    IReadOnlyList<TreeItem> GetTreeItems(TreeType treeType)
    {
        selectedId = selectionService.SelectedId.Id;

        using var model = modelMgr.UseModel();

        if (model.Nodes.TryGetValue(NodeId.FromId(selectedId), out var selectedNode))
        {
            Title = selectedNode.ShortName;
            Subtitle = treeType is TreeType.References ? "Nodes that use this node" : "Nodes that this node uses";
            return GetNodeItems(selectedNode, treeType);
        }
        if (model.Lines.TryGetValue(LineId.FromId(selectedId), out var selectedLine))
        {
            Title = $"{selectedLine.Source.ShortName}→{selectedLine.Target.ShortName}";
            Subtitle =
                treeType is TreeType.References
                    ? "Source nodes of this line's links"
                    : "Target nodes of this line's links";
            return GetLineItems(selectedLine, [.. selectedLine.Links], treeType);
        }

        Title = "No items found";
        Subtitle = "";
        return [];
    }

    // Returns one subtree for each line into the node (references) or out of the node
    // (dependencies) that carries at least one link actually ending at the node or a descendant
    // (other links just pass by on their way to some other node).
    static IReadOnlyList<TreeItem> GetNodeItems(Node node, TreeType treeType)
    {
        List<TreeItem> items = [];

        var lines = treeType is TreeType.References ? node.TargetLines : node.SourceLines;
        foreach (var line in lines)
        {
            var isLineForNode = line.Links.Any(link =>
            {
                var endpoint = treeType is TreeType.References ? link.Target : link.Source;
                return endpoint == node || endpoint.Ancestors().Contains(node);
            });
            if (!isLineForNode)
                continue;

            items.AddRange(GetLineItems(line, [.. line.Links], treeType));
        }

        if (!items.Any())
        {
            var text = treeType is TreeType.References ? "No references found" : "No dependencies found";
            items.Add(new TreeItem() { Text = text });
        }
        return items;
    }

    // Returns an item for the node at the far end of the line (the source for references, the
    // target for dependencies). Each tree branch traces one chain of lines that carry the root
    // line's links; the item's children continue that chain from the far node.
    static IReadOnlyList<TreeItem> GetLineItems(Line line, HashSet<Link> rootLinks, TreeType treeType)
    {
        var (farNode, nearNode) =
            treeType is TreeType.References ? (line.Source, line.Target) : (line.Target, line.Source);

        var farLines = treeType is TreeType.References ? farNode.TargetLines : farNode.SourceLines;
        List<Line> nextLines = [.. farLines.Where(l => l.Links.Any(rootLinks.Contains))];

        if (nearNode.Parent == farNode)
        {
            // A line from/to the direct parent adds no information; continue the chain past it.
            return nextLines.SelectMany(l => GetLineItems(l, rootLinks, treeType)).ToList();
        }

        // Children are created lazily on first expand, after the model lock has been released;
        // the captured lines/nodes may be stale if the model has been re-parsed since.
        GetTreeItemChildren? getChildren = nextLines.Any()
            ? () => [.. nextLines.SelectMany(l => GetLineItems(l, rootLinks, treeType))]
            : null;

        var linkCount = line.Links.Count(rootLinks.Contains);
        return [new TreeItem(farNode, linkCount, getChildren)];
    }
}
