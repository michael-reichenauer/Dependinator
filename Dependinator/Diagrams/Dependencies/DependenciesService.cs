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
            leftTree = new(TreeSide.Left, this, model.Root);
            rightTree = new(TreeSide.Right, this, model.Root);

            if (!model.TryGetNode(selectedId, out var node)) return;

            var nodeItem = leftTree.AddNode(node);
            nodeItem.ExpandAncestors();
            leftTree.Selected = nodeItem;
        }

        IsShowExplorer = true;
        applicationEvents.TriggerUIStateChanged();
    }

    public void ItemSelected(TreeItem item)
    {
        if (item == null) return;

        if (item.Side == TreeSide.Left) LeftItemSelected(item);
        if (item.Side == TreeSide.Right) RightItemSelected(item);

        applicationEvents.TriggerUIStateChanged();
    }

    void LeftItemSelected(TreeItem item)
    {
        Log.Info("Left item selected", item.Title);
        leftTree.Selected?.SetIsSelected(false);
        item.SetIsSelected(true);

        using var model = modelService.UseModel();
        rightTree = new(TreeSide.Right, this, model.Root);
        if (!model.TryGetNode(item.NodeId, out var node)) return;

        Log.Info("Reference from", node.Name);
        node.SourceLines.Select(l => l.Target).ForEach(n =>
       {
           Log.Info("  to", n.Name);
           var item = rightTree.AddNode(n);
           item.ExpandAncestors();
       });
    }

    void RightItemSelected(TreeItem item)
    {
        Log.Info("Right item selected", item.Title);
        rightTree.Selected?.SetIsSelected(false);
        item.SetIsSelected(true);

        using var model = modelService.UseModel();
        leftTree = new(TreeSide.Right, this, model.Root);

        if (!model.TryGetNode(item.NodeId, out var node)) return;

        Log.Info("Reference to", node.Name);
        node.TargetLines.Select(l => l.Source).ForEach(n =>
        {
            Log.Info("  from", n.Name);
            var item = leftTree.AddNode(n);
            item.ExpandAncestors();
        });
    }

    internal void SetChildrenItems(TreeItem treeItem)
    {
        using var model = modelService.UseModel();
        if (model.TryGetNode(treeItem.NodeId, out var node))
        {
            node.Children.ForEach(child => treeItem.AddChildNode(child));
        }
    }


    public void HideExplorer()
    {
        Log.Info("HideExplorer");
        IsShowExplorer = false;

        using (var model = modelService.UseModel())
        {
            leftTree = new(TreeSide.Left, this, model.Root);
            rightTree = new(TreeSide.Right, this, model.Root);
        }

        applicationEvents.TriggerUIStateChanged();
    }
}