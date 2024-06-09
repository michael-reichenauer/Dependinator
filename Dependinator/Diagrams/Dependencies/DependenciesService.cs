using Dependinator.Models;

namespace Dependinator.Diagrams;


interface IDependenciesService
{
    bool IsShowExplorer { get; }

    Tree TreeData(TreeSide side);

    void ShowExplorer();
    void HideExplorer();
}


[Scoped]
class DependenciesService(
    ISelectionService selectionService,
    IApplicationEvents applicationEvents,
    IModelService modelService) : IDependenciesService
{
    Tree leftTree = null!;
    Tree rightTree = null!;

    public bool IsShowExplorer { get; private set; }

    public Tree TreeData(TreeSide side)
    {
        if (side == TreeSide.Left) return leftTree;
        return rightTree;
    }


    public void ShowExplorer()
    {
        var selectedId = selectionService.SelectedId.Id;

        using (var model = modelService.UseModel())
        {
            leftTree = new(this, TreeSide.Left, model.Root);
            rightTree = new(this, TreeSide.Right, model.Root);

            if (!model.TryGetNode(selectedId, out var selectedNode)) return;

            leftTree.IsSelected = true;
            var selectedItem = leftTree.AddNode(selectedNode);
            leftTree.Selected = selectedItem;
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