using Dependinator.Models;
using MudBlazor;

namespace Dependinator.Diagrams;


public enum TreeSide { Left, Right }

internal class TreeItem
{
    public string Title { get; set; } = "";
    public string Icon { get; set; } = Icons.Material.Filled.Folder;
    public bool CanExpand => Items.Any();
    public bool IsExpanded { get; set; }
    public HashSet<TreeItem> Items { get; set; } = [];
    public TreeItem? Parent { get; set; }
    public Node Node { get; set; } = null!;

    public IEnumerable<TreeItem> Ancestors()
    {
        var current = this;
        while (current.Parent != null)
        {
            yield return current.Parent;
            current = current.Parent;
        }
    }
}

internal class TreeData
{
    public HashSet<TreeItem> Items { get; set; } = new();
    public TreeItem Selected { get; set; } = null!;
}




interface IDependenciesService
{
    bool IsShowExplorer { get; }

    TreeData TreeData(TreeSide side);

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
    TreeData left = new();
    TreeData right = new();

    public bool IsShowExplorer { get; private set; }

    public TreeData TreeData(TreeSide side)
    {
        if (side == TreeSide.Left) return left;
        return right;
    }


    public async Task<HashSet<TreeItem>> LoadSubTreeAsync(TreeItem parentNode)
    {
        await Task.CompletedTask;
        return parentNode.Items;
    }



    public void ShowExplorer()
    {
        var selectedId = selectionService.SelectedId.Id;

        left = new();
        right = new();

        using (var model = modelService.UseModel())
        {
            if (!model.TryGetNode(selectedId, out var node)) return;

            var leftRoot = ToItem(model.Root);
            var rightRoot = ToItem(model.Root);
            left.Items.Add(leftRoot);
            right.Items.Add(rightRoot);

            AddNode(leftRoot, node);
            node.SourceLinks.ForEach(l => AddNode(rightRoot, l.Target));

            SelectNode(left, node);
        }

        IsShowExplorer = true;
        applicationEvents.TriggerUIStateChanged();
    }


    private void SelectNode(TreeData left, Node node)
    {
        var item = FindItemWithNode(left.Items.First(), node);
        if (item == null) return;

        left.Selected = item;

        foreach (var parent in item.Ancestors())
        {
            parent.IsExpanded = true;
        }
    }

    public TreeItem? FindItemWithNode(TreeItem root, Node node)
    {
        if (root.Node == node) return root;

        return root.Items
            .Select(child => FindItemWithNode(child, node))
            .FirstOrDefault(result => result != null);
    }

    private void AddNode(TreeItem parent, Node node)
    {
        // Start from root, but skip root
        var ancestors = node.Ancestors().Reverse().Skip(1);
        var current = parent;

        foreach (var ancestor in ancestors)
        {
            var ancestorItem = current.Items.FirstOrDefault(n => n.Node.Name == ancestor.Name);
            if (ancestorItem != null)
            {   // Ancestor already added
                current = ancestorItem;
                continue;
            }

            // Add ancestor node to tree
            ancestorItem = ToItem(ancestor);
            ancestorItem.Parent = current;
            current.Items.Add(ancestorItem);
            current = ancestorItem;
        }

        // Add ancestor node to tree
        var nodeItem = ToItem(node);
        nodeItem.Parent = current;
        current.Items.Add(nodeItem);
    }

    public void HideExplorer()
    {
        Log.Info("HideExplorer");
        IsShowExplorer = false;
        left = new();
        right = new();

        applicationEvents.TriggerUIStateChanged();
    }

    TreeItem ToItem(Node node)
    {
        var name = node.IsRoot ? "<all>" : node.ShortName;
        return new TreeItem { Title = name, Icon = Icons.Material.Filled.Folder, Node = node };
    }
}