using System.Net.Sockets;
using Dependinator.DiagramIcons;
using Dependinator.Models;
using MudBlazor;

namespace Dependinator.Diagrams.Dependencies;

public enum TreeType
{
    References,
    Dependencies
}



interface IDependenciesService
{
    string TreeIcon { get; }
    IReadOnlyList<TreeItem> TreeItems { get; }

    void SetSelected(TreeItem selectedItem);
    void ShowNode(NodeId nodeId);
    void ShowReferences();
    void ShowDependencies();
    IReadOnlyList<Node> GetChildren(NodeId nodeId);
}

[Scoped]
class DependenciesService(
    IDialogService dialogService,
    ISelectionService selectionService,
    IApplicationEvents applicationEvents,
    IModelService modelService,
    IPanZoomService panZoomService) : IDependenciesService
{

    public IReadOnlyList<TreeItem> TreeItems { get; private set; } = [];

    public string TreeIcon => true ? Icon.DependenciesIcon : Icon.ReferencesIcon;

    public void ShowReferences()
    {
        TreeItems = GetTreeItems(TreeType.References);

        var options = new DialogOptions() { NoHeader = true, CloseOnEscapeKey = true, Position = DialogPosition.Custom };
        dialogService.ShowAsync<DependenciesTree>(null, options);
        applicationEvents.TriggerUIStateChanged();
    }


    public void ShowDependencies()
    {
        TreeItems = GetTreeItems(TreeType.Dependencies);

        var options = new DialogOptions() { NoHeader = true, CloseOnEscapeKey = true, Position = DialogPosition.Custom };
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
            if (!model.TryGetNode(selectedId, out var selectedNode)) return [];

            var items = GetNodeItems(selectedNode, treeType, null);
            treeItems.AddRange(items);
        }

        return treeItems;
    }

    IReadOnlyList<TreeItem> GetNodeItems(Node node, TreeType treeType, TreeItem? parentItem)
    {
        if (treeType == TreeType.References)
        {
            return GetNodeReferenceItems(node, parentItem);
        }
        else
        {
            return GetNodeDependencyItems(node, parentItem);
        }
    }


    private IReadOnlyList<TreeItem> GetNodeReferenceItems(Node node, TreeItem? parentItem)
    {
        List<TreeItem> items = [];

        foreach (var line in node.TargetLines)
        {
            var isToNodeOrChild = line.Links.Any(link =>
                link.Target == node ||
                link.Target.Ancestors().Contains(node));

            if (!isToNodeOrChild) continue;

            var referenceItems = GetLineReferenceItems(line, line, parentItem);
            items.AddRange(referenceItems);
        }
        return items;
    }


    private IReadOnlyList<TreeItem> GetLineReferenceItems(Line line, Line rootLine, TreeItem? parentItem)
    {
        var sourceTargetLines = line.Source.TargetLines
            .Where(stl => stl.Links.Any(l => rootLine.Links.Contains(l)));

        Func<TreeItem, IReadOnlyList<TreeItem>> getChildren = (itemParent) =>
            [.. sourceTargetLines.SelectMany(tsl => GetLineReferenceItems(tsl, rootLine, itemParent))];

        var hasChildren = sourceTargetLines.Any();
        return [ToTreeItem(line.Source, parentItem, hasChildren, getChildren)];
    }

    private IReadOnlyList<TreeItem> GetNodeDependencyItems(Node node, TreeItem? parentItem)
    {
        List<TreeItem> items = [];

        foreach (var line in node.SourceLines)
        {
            var isFromNodeOrChild = line.Links.Any(link =>
                link.Source == node ||
                link.Source.Ancestors().Contains(node));
            if (!isFromNodeOrChild) continue;

            var dependencyItems = GetLineDependencyItems(line, line, parentItem);
            items.AddRange(dependencyItems);
        }
        return items;
    }

    private IReadOnlyList<TreeItem> GetLineDependencyItems(Line line, Line rootLine, TreeItem? parentItem)
    {
        var targetSourceLines = line.Target.SourceLines
            .Where(stl => stl.Links.Any(l => rootLine.Links.Contains(l)));

        Func<TreeItem, IReadOnlyList<TreeItem>> getChildren = (itemParent) =>
            [.. targetSourceLines.SelectMany(tsl => GetLineDependencyItems(tsl, rootLine, itemParent))];
        var hasChildren = targetSourceLines.Any();
        return [ToTreeItem(line.Target, parentItem, hasChildren, getChildren)];
    }

    TreeItem ToTreeItem(Node node, TreeItem? parentItem, bool hasChildren, Func<TreeItem, IReadOnlyList<TreeItem>> getChildren)
    {
        return new TreeItem(this, parentItem, node, hasChildren, getChildren)
        {
            Text = node.ShortName,
            Icon = Dependinator.DiagramIcons.Icon.GetIcon(node.Icon),
        };
    }


    public void ShowNode(NodeId nodeId)
    {
        selectionService.Unselect();


        Pos pos = Pos.None;
        double zoom = 0;
        if (!modelService.UseNodeN(nodeId, node =>
        {
            (pos, zoom) = node.GetCenterPosAndZoom();
        })) return;

        panZoomService.PanZoomToAsync(pos, zoom).RunInBackground();
    }




    public void SetSelected(TreeItem selectedItem)
    {
        Log.Info($"ItemSelected: {selectedItem.Text}");
    }


    public IReadOnlyList<Node> GetChildren(NodeId nodeId)
    {
        using var model = modelService.UseModel();
        if (!model.TryGetNode(nodeId, out var node)) return [];

        return node.Children;
    }
}
