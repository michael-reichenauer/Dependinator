using Dependinator.Models;
using MudBlazor;

namespace Dependinator.Diagrams;


interface IDependenciesService
{
    bool IsShowExplorer { get; }

    Tree TreeData(TreeSide side);

    Task<HashSet<TreeItem>> LoadSubTreeAsync(TreeItem parentNode);

    void ShowExplorer();
    void HideExplorer();
}


[Scoped]
class DependenciesService(
    ISelectionService selectionService,
    IApplicationEvents applicationEvents,
    IModelService modelService) : IDependenciesService
{
    Tree leftTree = new();
    Tree rightTree = new();

    public bool IsShowExplorer { get; private set; }

    public Tree TreeData(TreeSide side)
    {
        if (side == TreeSide.Left) return leftTree;
        return rightTree;
    }


    public async Task<HashSet<TreeItem>> LoadSubTreeAsync(TreeItem parentNode)
    {
        await Task.CompletedTask;
        return parentNode.Items;
    }


    public void ShowExplorer()
    {
        var selectedId = selectionService.SelectedId.Id;

        leftTree = new();
        rightTree = new();

        using (var model = modelService.UseModel())
        {
            if (!model.TryGetNode(selectedId, out var node)) return;

            var leftRoot = ToItem(model.Root);
            var rightRoot = ToItem(model.Root);
            leftTree.Items.Add(leftRoot);
            rightTree.Items.Add(rightRoot);

            AddDecendantNode(leftRoot, node);
            node.SourceLinks.ForEach(l => AddDecendantNode(rightRoot, l.Target));

            SelectNode(leftTree, node);
        }

        IsShowExplorer = true;
        applicationEvents.TriggerUIStateChanged();
    }

    internal void SetAllItems(TreeItem treeItem)
    {
        Log.Info("SetAllItems", treeItem.Title);
        using var model = modelService.UseModel();
        if (model.TryGetNode(treeItem.NodeId, out var node))
        {
            node.Children.ForEach(child => AddChildNode(treeItem, child));
        }
        else
        {
            Log.Info("Node not found", treeItem.NodeId);
        }
    }

    private void SelectNode(Tree tree, Node node)
    {
        var item = FindItemWithNode(tree.Items.First(), node);
        if (item == null) return;
        Log.Info("SelectNode", item.Title);

        tree.Selected = item;
        item.Ancestors().ForEach(a => a.SetIsExpanded(true));
    }

    public TreeItem? FindItemWithNode(TreeItem root, Node node)
    {
        if (root.NodeId == node.Id) return root;

        return root.Items
            .Select(child => FindItemWithNode(child, node))
            .FirstOrDefault(result => result != null);
    }


    TreeItem AddChildNode(TreeItem parent, Node node)
    {
        // Check if node already added
        var item = parent.Items.FirstOrDefault(n => n.NodeId == node.Id);
        if (item != null) return item;

        var nodeItem = ToItem(node);
        nodeItem.Parent = parent;
        parent.AddItem(nodeItem);
        return nodeItem;
    }


    TreeItem AddDecendantNode(TreeItem rootItem, Node node)
    {
        // Add Ancestors to the node
        // Start from root, but skip root
        var ancestors = node.Ancestors().Reverse().Skip(1);
        var current = rootItem;
        foreach (var ancestor in ancestors)
        {
            var ancestorItem = current.Items.FirstOrDefault(n => n.NodeId == ancestor.Id);
            if (ancestorItem != null)
            {   // Ancestor already added
                current = ancestorItem;
                continue;
            }

            // Add ancestor node to tree
            current = AddChildNode(current, ancestor);
        }

        // Add node to its parent
        return AddChildNode(current, node);
    }

    public void HideExplorer()
    {
        Log.Info("HideExplorer");
        IsShowExplorer = false;
        leftTree = new();
        rightTree = new();

        applicationEvents.TriggerUIStateChanged();
    }


    TreeItem ToItem(Node node)
    {
        var name = node.IsRoot ? "<all>" : node.ShortName;
        var nodeChildrenCount = node.Children.Count();

        return new TreeItem(this)
        {
            Title = name,
            Icon = Dependinator.DiagramIcons.Icon.GetIcon(node.Type.Text),
            NodeId = node.Id,
            NodeChildrenCount = nodeChildrenCount,
        };
    }
}