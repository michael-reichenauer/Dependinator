using Dependinator.Models;

namespace Dependinator.Diagrams;


interface IDependenciesService
{
    bool IsShowExplorer { get; }
    string Icon { get; }

    Tree TreeData(TreeSide side);

    void ShowExplorer(TreeSide selectedSide);
    void SwitchSides();
    void HideExplorer();
}


[Scoped]
class DependenciesService(
    ISelectionService selectionService,
    IApplicationEvents applicationEvents,
    IModelService modelService) : IDependenciesService
{
    public const string Dependencies = MudBlazor.Icons.Material.Outlined.Polyline;
    public const string References = "<g><rect fill=\"none\" height=\"24\" width=\"24\"/></g><g transform=\"rotate(180,12,12)\"><path d=\"M15,16v1.26l-6-3v-3.17L11.7,8H16V2h-6v4.9L7.3,10H3v6h5l7,3.5V22h6v-6H15z M12,4h2v2h-2V4z M7,14H5v-2h2V14z M19,20h-2v-2 h2V20z\"/></g>";


    Tree leftTree = null!;
    Tree rightTree = null!;

    public bool IsShowExplorer { get; private set; }

    public string Icon => leftTree?.IsSelected == true ? Dependencies : References;


    public Tree TreeData(TreeSide side)
    {
        if (side == TreeSide.Left) return leftTree;
        return rightTree;
    }

    public void SwitchSides()
    {
        var tree = leftTree.IsSelected ? leftTree : rightTree;
        var selectedItem = tree.Selected;

        var selectedId = selectedItem.NodeId;
        var otherTreeSide = tree.OtherTree.Side;
        ShowNodeExplorer(otherTreeSide, selectedId.Value);
    }

    public void ShowExplorer(TreeSide selectedSide)
    {
        var selectedId = selectionService.SelectedId.Id;

        ShowNodeExplorer(selectedSide, selectedId);
    }

    void ShowNodeExplorer(TreeSide selectedSide, string selectedId)
    {
        using (var model = modelService.UseModel())
        {
            leftTree = new(this, TreeSide.Left, model.Root);
            rightTree = new(this, TreeSide.Right, model.Root);

            if (!model.TryGetNode(selectedId, out var selectedNode)) return;

            var tree = selectedSide == TreeSide.Left ? leftTree : rightTree;

            tree.IsSelected = true;
            var selectedItem = tree.AddNode(selectedNode);
            tree.Selected = selectedItem;
        }

        IsShowExplorer = true;
        applicationEvents.TriggerUIStateChanged();
    }

    public TreeItem ItemSelected(TreeItem selectedItem)
    {
        using var model = modelService.UseModel();
        if (!model.TryGetNode(selectedItem.NodeId, out var selectedNode)) return selectedItem;

        var isSwitchSide = !selectedItem.Tree.IsSelected;

        leftTree.ClearSelection();
        rightTree.ClearSelection();

        if (isSwitchSide)
        {   // Switching side, lets refresh new selected side items.
            selectedItem = SetSelectedSideItems(selectedItem, selectedNode, model.Root);
        }

        selectedItem.Tree.SetSelectedItem(selectedItem);

        SetOtherSideItems(selectedItem, selectedNode, model.Root);

        applicationEvents.TriggerUIStateChanged();
        return selectedItem;
    }

    static TreeItem SetSelectedSideItems(TreeItem selectedItem, Node selectedNode, Node root)
    {
        var thisTree = selectedItem.Tree;
        thisTree.EmptyTo(root);
        selectedItem.Tree.IsSelected = true;

        return thisTree.AddNode(selectedNode);
    }

    static void SetOtherSideItems(TreeItem selectedItem, Node selectedNode, Node root)
    {
        var otherTree = selectedItem.Tree.OtherTree;
        otherTree.EmptyTo(root);

        // Get all peer noded for seleced node, SelectedPeers 
        LinkNodes(selectedItem.Tree, selectedNode).ForEach(n =>
        {
            otherTree.SelectedPeers.Add(n.Id);
            n.Ancestors().ForEach(a => otherTree.SelectedPeers.Add(a.Id));
        });

        HashSet<NodeId> addedNodes = [];
        LineNodes(selectedItem.Tree, selectedNode).ForEach(n =>
        {
            addedNodes.Add(n.Id);
            if (selectedNode.Parent == n)
            {
                SetOtherAncestorSideItems(selectedItem, selectedNode, n, addedNodes);
                return;
            }

            otherTree.AddNode(n);
        });
    }

    static void SetOtherAncestorSideItems(TreeItem selectedItem, Node selectedNode, Node otherNode, HashSet<NodeId> addedNodes)
    {
        var otherTree = selectedItem.Tree.OtherTree;
        LineNodes(selectedItem.Tree, otherNode).ForEach(n =>
        {
            if (!otherTree.IsNodeIncluded(n)) return;
            if (addedNodes.Contains(n.Id)) return;
            addedNodes.Add(n.Id);

            if (selectedNode.Ancestors().Contains(n))
            {
                SetOtherAncestorSideItems(selectedItem, selectedNode, n, addedNodes);
                return;
            }

            otherTree.AddNode(n);
        });
    }

    static IEnumerable<Node> LineNodes(Tree tree, Node node)
    {
        if (tree.Side == TreeSide.Left)
            return node.SourceLines.Select(l => l.Target);

        return node.TargetLines.Select(l => l.Source);
    }

    static IEnumerable<Node> LinkNodes(Tree tree, Node node)
    {
        if (tree.Side == TreeSide.Left)
            return node.SourceLines.SelectMany(l => l.Links.Select(link => link.Target));

        return node.TargetLines.SelectMany(l => l.Links.Select(link => link.Source));
    }

    internal IReadOnlyList<Node> GetChildren(NodeId nodeId)
    {
        using var model = modelService.UseModel();
        if (!model.TryGetNode(nodeId, out var node)) return [];

        return node.Children;
    }


    public void HideExplorer()
    {
        IsShowExplorer = false;

        using (var model = modelService.UseModel())
        {
            leftTree = new(this, TreeSide.Left, model.Root);
            rightTree = new(this, TreeSide.Right, model.Root);
        }

        applicationEvents.TriggerUIStateChanged();
    }
}