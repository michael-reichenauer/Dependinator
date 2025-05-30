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
    bool IsShowExplorer { get; }

    string Title { get; }
    string TreeIcon { get; }
    IReadOnlyList<TreeItem> TreeItems { get; }

    void SetSelected(TreeItem selectedItem);
    void ShowNode(NodeId nodeId);
    void ShowReferences();
    void ShowDependencies();
    void Clicked(PointerId pointerId);
}

[Scoped]
class DependenciesService(
    ISelectionService selectionService,
    IApplicationEvents applicationEvents,
    IModelService modelService,
    IPanZoomService panZoomService
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

    public async void ShowNode(NodeId nodeId)
    {
        Close();
        selectionService.Unselect();
        applicationEvents.TriggerUIStateChanged();
        await Task.Yield();

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
