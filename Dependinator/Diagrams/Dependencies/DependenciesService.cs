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

        leftTree.Selected?.SetIsSelected(false);
        rightTree.Selected?.SetIsSelected(false);
        leftTree.IsSelected = false;
        rightTree.IsSelected = false;

        item.SetIsSelected(true);

        if (item.Side == TreeSide.Left) LeftItemSelected(item, node, model.Root);
        if (item.Side == TreeSide.Right) RightItemSelected(item, node, model.Root);

        applicationEvents.TriggerUIStateChanged();
    }

    void LeftItemSelected(TreeItem item, Node node, Node root)
    {
        Log.Info("Left item selected", item.Title);
        leftTree.IsSelected = true;

        rightTree = new(this, TreeSide.Right, root);
        node.SourceLines
           .SelectMany(l => l.Links.Select(link => link.Target))
           .ForEach(n =>
            {
                rightTree.SelectedPeers.Add(n.Id);
                n.Ancestors().ForEach(a => rightTree.SelectedPeers.Add(a.Id));
            });

        node.SourceLines.Select(l => l.Target)
            .ForEach(n =>
            {
                if (node.Parent == n)
                {
                    AddAncestorTargets(rightTree, node, n);
                    return;
                }

                rightTree.AddNode(n);
            });
    }


    void RightItemSelected(TreeItem item, Node node, Node root)
    {
        Log.Info("Right item selected", item.Title);
        rightTree.IsSelected = true;

        leftTree = new(this, TreeSide.Right, root);

        node.TargetLines
            .SelectMany(l => l.Links.Select(link => link.Source))
            .ForEach(n =>
            {
                leftTree.SelectedPeers.Add(n.Id);
                n.Ancestors().ForEach(a => leftTree.SelectedPeers.Add(a.Id));
            });

        Log.Info("Reference to", node.Name);
        node.TargetLines.Select(l => l.Source).ForEach(n =>
        {
            Log.Info("  from", n.Name);
            if (node.Parent == n)
            {
                AddAncestorSources(leftTree, node, n);
                return;
            }
            leftTree.AddNode(n);
        });

    }

    void AddAncestorTargets(Tree rightTree, Node node, Node otherNode)
    {
        otherNode.SourceLines.Select(l => l.Target).ForEach(n =>
        {
            if (node.Ancestors().Contains(n))
            {
                AddAncestorTargets(rightTree, node, n);
                return;
            }

            rightTree.AddNode(n);
        });
    }

    void AddAncestorSources(Tree leftTree, Node node, Node otherNode)
    {
        otherNode.TargetLines.Select(l => l.Source).ForEach(n =>
        {
            if (node.Ancestors().Contains(n))
            {
                AddAncestorSources(leftTree, node, n);
                return;
            }

            leftTree.AddNode(n);
        });
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