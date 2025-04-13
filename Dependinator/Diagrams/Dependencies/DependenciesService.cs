using Dependinator.DiagramIcons;
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
    string TreeIcon { get; }
    IReadOnlyList<TreeItem> TreeItems { get; }

    void SetSelected(TreeItem selectedItem);
    void ShowNode(NodeId nodeId);
    void ShowReferences();
    void ShowDependencies();
}

[Scoped]
class DependenciesService(
    IDialogService dialogService,
    ISelectionService selectionService,
    IApplicationEvents applicationEvents,
    IModelService modelService,
    IPanZoomService panZoomService
) : IDependenciesService
{
    public IReadOnlyList<TreeItem> TreeItems { get; private set; } = [];

    public string TreeIcon => true ? Icon.DependenciesIcon : Icon.ReferencesIcon;

    public async void ShowNode(NodeId nodeId)
    {
        selectionService.Unselect();

        Pos pos = Pos.None;
        double zoom = 0;
        if (
            !modelService.UseNodeN(
                nodeId,
                node =>
                {
                    (pos, zoom) = node.GetCenterPosAndZoom();
                }
            )
        )
            return;

        await panZoomService.PanZoomToAsync(pos, zoom);
        selectionService.Select(nodeId);
        applicationEvents.TriggerUIStateChanged();
    }

    public void SetSelected(TreeItem selectedItem)
    {
        if (selectedItem is null)
            return;
        Log.Info($"ItemSelected: {selectedItem.Text}");
    }

    public void ShowReferences()
    {
        TreeItems = GetTreeItems(TreeType.References);

        var options = new DialogOptions()
        {
            NoHeader = true,
            CloseOnEscapeKey = true,
            Position = DialogPosition.Custom,
        };
        dialogService.ShowAsync<DependenciesTree>(null, options);
        applicationEvents.TriggerUIStateChanged();
    }

    public void ShowDependencies()
    {
        TreeItems = GetTreeItems(TreeType.Dependencies);

        var options = new DialogOptions()
        {
            NoHeader = true,
            CloseOnEscapeKey = true,
            Position = DialogPosition.Custom,
        };
        dialogService.ShowAsync<DependenciesTree>(null, options);
        applicationEvents.TriggerUIStateChanged();
    }

    IReadOnlyList<TreeItem> GetTreeItems(TreeType treeType)
    {
        List<TreeItem> treeItems = [];
        Log.Info($"GetTreeItems: {treeType}");

        var selectedId = selectionService.SelectedId.Id;

        using (var model = modelService.UseModel())
        {
            if (!model.TryGetNode(selectedId, out var selectedNode))
                return [];

            var items = GetNodeItems(selectedNode, treeType);
            treeItems.AddRange(items);
        }

        return treeItems;
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
