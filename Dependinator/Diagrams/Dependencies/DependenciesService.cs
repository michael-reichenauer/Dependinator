using Dependinator.Models;

namespace Dependinator.Diagrams.Dependencies;


interface IDependenciesService
{
    string Icon { get; }

    Tree TreeData();

    void ShowExplorer();
    void ToggleShowTrees();
    void SwitchSides();
    void HideExplorer();
    void ShowNode(NodeId nodeId);
}

[Scoped]
class DependenciesService(
    ISelectionService selectionService,
    IApplicationEvents applicationEvents,
    IModelService modelService,
    IPanZoomService panZoomService) : IDependenciesService
{
    public const string Dependencies = MudBlazor.Icons.Material.Outlined.Polyline;
    public const string References = "<g><rect fill=\"none\" height=\"24\" width=\"24\"/></g><g transform=\"rotate(180,12,12)\"><path d=\"M15,16v1.26l-6-3v-3.17L11.7,8H16V2h-6v4.9L7.3,10H3v6h5l7,3.5V22h6v-6H15z M12,4h2v2h-2V4z M7,14H5v-2h2V14z M19,20h-2v-2 h2V20z\"/></g>";


    Tree currentTree = null!;

    public string Icon => currentTree?.IsSelected == true ? Dependencies : References;


    public Tree TreeData()
    {
        if (currentTree is null)
        {
            var selectedId = selectionService.SelectedId.Id;

            ShowNodeExplorer(selectedId);
        }

        return currentTree;
    }

    public void ShowNode(NodeId nodeId)
    {
        selectionService.Unselect();
        applicationEvents.TriggerUIStateChanged();

        Pos pos = Pos.None;
        double zoom = 0;
        if (!modelService.UseNodeN(nodeId, node =>
        {
            (pos, zoom) = node.GetCenterPosAndZoom();
        })) return;

        panZoomService.PanZoomToAsync(pos, zoom).RunInBackground();
    }

    public void ShowExplorer()
    {
        Log.Debug($"ShowExplorer");
        var selectedId = selectionService.SelectedId.Id;

        ShowNodeExplorer(selectedId);
    }

    public void ToggleShowTrees()
    {
        applicationEvents.TriggerUIStateChanged();
    }

    public void HideExplorer()
    {
        using (var model = modelService.UseModel())
        {
            currentTree = new(this, model.Root);
        }

        applicationEvents.TriggerUIStateChanged();
    }

    public void SwitchSides()
    {
        var tree = currentTree;
        var selectedItem = tree.SelectedItem;

        var selectedId = selectedItem.NodeId;
        ShowNodeExplorer(selectedId.Value);
    }

    void ShowNodeExplorer(string selectedId)
    {
        using (var model = modelService.UseModel())
        {
            currentTree = new(this, model.Root);

            if (!model.TryGetNode(selectedId, out var selectedNode)) return;

            var activeTree = GetActiveTree();

            activeTree.IsSelected = true;
            var selectedItem = activeTree.AddNode(selectedNode);
            activeTree.SelectedItem = selectedItem;
        }

        applicationEvents.TriggerUIStateChanged();
    }


    public TreeItem ItemSelected(TreeItem selectedItem)
    {
        using var model = modelService.UseModel();
        if (!model.TryGetNode(selectedItem.NodeId, out var selectedNode)) return selectedItem;

        var isSwitchSide = !selectedItem.Tree.IsSelected;

        currentTree.ClearSelection();

        if (isSwitchSide)
        {   // Switching side, lets refresh new selected side items.
            selectedItem = SetSelectedSideItems(selectedItem, selectedNode, model.Root);
        }

        selectedItem.Tree.SetSelectedItem(selectedItem);

        //SetOtherSideItems(selectedItem, selectedNode, model.Root);

        applicationEvents.TriggerUIStateChanged();
        return selectedItem;
    }

    Tree GetActiveTree() => currentTree;

    static TreeItem SetSelectedSideItems(TreeItem selectedItem, Node selectedNode, Node root)
    {
        var thisTree = selectedItem.Tree;
        thisTree.EmptyTo(root);
        selectedItem.Tree.IsSelected = true;

        return thisTree.AddNode(selectedNode);
    }


    internal IReadOnlyList<Node> GetChildren(NodeId nodeId)
    {
        using var model = modelService.UseModel();
        if (!model.TryGetNode(nodeId, out var node)) return [];

        return node.Children;
    }
}
