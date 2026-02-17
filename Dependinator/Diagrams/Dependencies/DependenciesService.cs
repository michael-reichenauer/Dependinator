using Dependinator.Diagrams.Icons;
using Dependinator.Models;
using MudBlazor;

namespace Dependinator.Diagrams.Dependencies;

public enum TreeType
{
    References,
    Dependencies,
}

interface IDependenciesService
{
    bool IsShowExplorer { get; }

    string Title { get; }
    string TreeIcon { get; }
    IReadOnlyList<TreeItem> TreeItems { get; }

    void SetSelected(TreeItem selectedItem);
    Task ShowNodeAsync(NodeId nodeId);
    bool CanShowEditor(NodeId nodeId);
    Task ShowEditorAsync(NodeId nodeId);
    void ShowDirectLine(NodeId nodeId);
    bool TryGetLine(LineId lineId, out Line line);
    void HideDirectLine(LineId lineId);
    void ShowReferences();
    void ShowDependencies();
    void Clicked(PointerId pointerId);
}

[Scoped]
class DependenciesService(
    ISelectionService selectionService,
    IApplicationEvents applicationEvents,
    IModelService modelService,
    INavigationService navigationService
) : IDependenciesService
{
    private string selectedId = "";
    TreeType treeType = TreeType.References;
    public IReadOnlyList<TreeItem> TreeItems { get; private set; } = [];

    public string Title { get; private set; } = "";
    public string TreeIcon => treeType == TreeType.Dependencies ? Icon.DependenciesIcon : Icon.ReferencesIcon;

    public bool IsShowExplorer { get; private set; }

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

        using var model = modelService.UseModel();

        if (!model.TryGetNode(sourceId, out var sourceNode))
            return;
        if (!model.TryGetNode(targetId, out var targetNode))
            return;

        var directLineId = LineId.FromDirect(sourceNode.Name, targetNode.Name);
        if (model.TryGetLine(directLineId, out var existingLine))
            return;

        var ancestor = sourceNode.LowestCommonAncestor(targetNode);
        var directLine = new Line(sourceNode, targetNode, isDirect: true, id: directLineId)
        {
            RenderAncestor = ancestor,
            IsHidden = false,
        };

        ancestor.AddDirectLine(directLine);
        model.AddLine(directLine);

        model.ClearCachedSvg();
        applicationEvents.TriggerUIStateChanged();
    }

    public bool TryGetLine(LineId lineId, out Line line)
    {
        using var model = modelService.UseModel();
        return model.TryGetLine(lineId.Value, out line);
    }

    public void HideDirectLine(LineId lineId)
    {
        var shouldUnselect = selectionService.SelectedId.IsLine && selectionService.SelectedId.Id == lineId.Value;

        using var model = modelService.UseModel();

        if (!model.TryGetLine(lineId, out var line))
            return;

        line.RenderAncestor?.RemoveDirectLine(line);
        line.RenderAncestor = null;
        line.Target.Remove(line);
        line.Source.Remove(line);
        model.Items.Remove(line.Id);
        model.ClearCachedSvg();

        if (shouldUnselect)
            selectionService.Unselect();
        applicationEvents.TriggerUIStateChanged();
    }

    public async Task ShowNodeAsync(NodeId nodeId)
    {
        Close();
        await navigationService.ShowNodeAsync(nodeId);
    }

    public bool CanShowEditor(NodeId nodeId)
    {
        return modelService.UseNodeN(
            nodeId,
            n =>
            {
                return n.FileSpanOrParentSpan is not null;
            }
        );
    }

    public async Task ShowEditorAsync(NodeId nodeId)
    {
        await navigationService.ShowEditor(nodeId);
    }

    private void Close()
    {
        IsShowExplorer = false;
        selectedId = "";
    }

    public void SetSelected(TreeItem selectedItem)
    {
        if (selectedItem is null)
            return;
        Log.Info($"ItemSelected: {selectedItem.Text}");
    }

    public void ShowReferences()
    {
        selectedId = "";
        treeType = TreeType.References;
        TreeItems = GetTreeItems(treeType);

        IsShowExplorer = true;
        applicationEvents.TriggerUIStateChanged();
    }

    public void ShowDependencies()
    {
        selectedId = "";
        treeType = TreeType.Dependencies;
        TreeItems = GetTreeItems(treeType);

        IsShowExplorer = true;
        applicationEvents.TriggerUIStateChanged();
    }

    IReadOnlyList<TreeItem> GetTreeItems(TreeType treeType)
    {
        List<TreeItem> treeItems = [];
        Log.Info($"GetTreeItems: {treeType}");

        selectedId = selectionService.SelectedId.Id;

        using (var model = modelService.UseModel())
        {
            if (model.TryGetNode(selectedId, out var selectedNode))
            {
                Title = selectedNode.HtmlShortName;
                var items = GetNodeItems(selectedNode, treeType);
                treeItems.AddRange(items);
                return treeItems;
            }
            if (model.TryGetLine(selectedId, out var selectedLine))
            {
                Title = selectedLine.HtmlShortName;
                var items = GetLineItems(selectedLine, treeType);
                treeItems.AddRange(items);
                return treeItems;
            }
        }

        Title = "No items found";
        return [];
    }

    static IReadOnlyList<TreeItem> GetNodeItems(Node node, TreeType treeType)
    {
        if (treeType == TreeType.References)
        {
            return GetNodeReferenceItems(node);
        }
        else
        {
            return GetNodeDependencyItems(node);
        }
    }

    static IReadOnlyList<TreeItem> GetLineItems(Line line, TreeType treeType)
    {
        if (treeType == TreeType.References)
        {
            return GetLineReferenceItems(line, line, null);
        }
        else
        {
            return GetLineDependencyItems(line, line, null);
        }
    }

    static IReadOnlyList<TreeItem> GetNodeReferenceItems(Node node)
    {
        List<TreeItem> items = [];

        foreach (var line in node.TargetLines)
        {
            var isToNodeOrChild = line.Links.Any(link => link.Target == node || link.Target.Ancestors().Contains(node));
            if (!isToNodeOrChild)
                continue;

            var referenceItems = GetLineReferenceItems(line, line, null);
            items.AddRange(referenceItems);
        }
        if (!items.Any())
        {
            items.Add(new TreeItem() { Text = "No references found" });
        }
        return items;
    }

    static IReadOnlyList<TreeItem> GetNodeDependencyItems(Node node)
    {
        List<TreeItem> items = [];

        foreach (var line in node.SourceLines)
        {
            var isFromNodeOrChild = line.Links.Any(link =>
                link.Source == node || link.Source.Ancestors().Contains(node)
            );
            if (!isFromNodeOrChild)
                continue;

            var dependencyItems = GetLineDependencyItems(line, line, null);
            items.AddRange(dependencyItems);
        }

        if (!items.Any())
        {
            items.Add(new TreeItem() { Text = "No dependencies found" });
        }
        return items;
    }

    static IReadOnlyList<TreeItem> GetLineReferenceItems(Line line, Line rootLine, TreeItem? parentItem)
    {
        var sourceTargetLines = line.Source.TargetLines.Where(stl => stl.Links.Any(l => rootLine.Links.Contains(l)));

        if (line.Target.Parent == line.Source)
        {
            // If the source is the parent of the target, we need to get the source lines of the source
            return sourceTargetLines.SelectMany(l => GetLineReferenceItems(l, rootLine, parentItem)).ToList();
        }

        GetTreeItemChildren? getChildren = sourceTargetLines.Any()
            ? (itemParent) => [.. sourceTargetLines.SelectMany(tsl => GetLineReferenceItems(tsl, rootLine, itemParent))]
            : null;

        return [new TreeItem(line.Source, parentItem, getChildren)];
    }

    static IReadOnlyList<TreeItem> GetLineDependencyItems(Line line, Line rootLine, TreeItem? parentItem)
    {
        var targetSourceLines = line.Target.SourceLines.Where(stl => stl.Links.Any(l => rootLine.Links.Contains(l)));
        if (line.Source.Parent == line.Target)
        {
            // If the target is the parent of the source, we need to get the target lines of the target
            return targetSourceLines.SelectMany(l => GetLineDependencyItems(l, rootLine, parentItem)).ToList();
        }

        GetTreeItemChildren? getChildren = targetSourceLines.Any()
            ? (itemParent) =>
                [.. targetSourceLines.SelectMany(tsl => GetLineDependencyItems(tsl, rootLine, itemParent))]
            : null;

        return [new TreeItem(line.Target, parentItem, getChildren)];
    }
}
