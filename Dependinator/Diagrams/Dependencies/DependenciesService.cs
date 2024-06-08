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
class DependenciesService : IDependenciesService
{
    readonly ISelectionService selectionService;
    readonly IApplicationEvents applicationEvents;
    readonly IModelService modelService;

    Tree leftTree = null!;
    Tree rightTree = null!;

    public DependenciesService(
        ISelectionService selectionService,
        IApplicationEvents applicationEvents,
        IModelService modelService)
    {
        this.selectionService = selectionService;
        this.applicationEvents = applicationEvents;
        this.modelService = modelService;
    }


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

            if (!model.TryGetNode(selectedId, out var node)) return;

            leftTree.IsSelected = true;
            var item = leftTree.AddNode(node);
            leftTree.Selected = item;
        }

        IsShowExplorer = true;
        applicationEvents.TriggerUIStateChanged();
    }

    public void ItemSelected(TreeItem item)
    {
        if (item == null) return;

        using var model = modelService.UseModel();
        if (!model.TryGetNode(item.NodeId, out var node)) return;

        leftTree.ClearSelection();
        rightTree.ClearSelection();

        item.Tree.SetSelectedItem(item);

        SetOtherSideItems(item, node, model.Root);

        applicationEvents.TriggerUIStateChanged();
    }

    void SetOtherSideItems(TreeItem item, Node node, Node root)
    {
        Log.Info("Item selected", item.Title);
        var otherTree = item.Tree.OtherTree;

        otherTree.EmptyTo(root);
        LinkNodes(item.Tree, node).ForEach(n =>
        {
            otherTree.SelectedPeers.Add(n.Id);
            n.Ancestors().ForEach(a => otherTree.SelectedPeers.Add(a.Id));
        });

        HashSet<NodeId> addedNodes = [];
        LineNodes(item.Tree, node).ForEach(n =>
        {
            addedNodes.Add(n.Id);
            if (node.Parent == n)
            {
                SetOtherAncestorSideItems(item, node, n, addedNodes);
                return;
            }

            otherTree.AddNode(n);
        });
    }

    void SetOtherAncestorSideItems(TreeItem item, Node node, Node otherNode, HashSet<NodeId> addedNodes)
    {
        var otherTree = item.Tree.OtherTree;
        LineNodes(item.Tree, otherNode).ForEach(n =>
        {
            if (!otherTree.IsNodeIncluded(n)) return;
            if (addedNodes.Contains(n.Id)) return;
            addedNodes.Add(n.Id);

            if (node.Ancestors().Contains(n))
            {
                SetOtherAncestorSideItems(item, node, n, addedNodes);
                return;
            }

            otherTree.AddNode(n);
        });
    }

    IEnumerable<Node> LineNodes(Tree tree, Node node)
    {
        if (tree.Side == TreeSide.Left)
            return node.SourceLines.Select(l => l.Target);

        return node.TargetLines.Select(l => l.Source);
    }


    IEnumerable<Node> LinkNodes(Tree tree, Node node)
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