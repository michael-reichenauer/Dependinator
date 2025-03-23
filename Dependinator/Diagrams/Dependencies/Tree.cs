using Dependinator.Models;

namespace Dependinator.Diagrams.Dependencies;

internal class Tree
{
    TreeItem rootItem;
    TreeItem selected = null!;

    public Tree(DependenciesService service, Node root)
    {
        Service = service;
        rootItem = TreeItem.CreateTreeItem(this, null, root);
        TreeItems.Add(rootItem);
    }


    public DependenciesService Service { get; }
    public List<TreeItem> TreeItems { get; } = [];
    public bool IsSelected { get; set; }
    public HashSet<NodeId> SelectedPeers { get; } = [];

    public string Title => IsSelected ? SelectedItem?.Text! ?? "" : "";



    // Set by the UI, when a tree item is selected. When starting to show dialog, null is sometimes set.
    public TreeItem SelectedItem
    {
        get => selected!;
        set
        {
            Log.Info("Selected item: " + value?.Text);
            if (value == null || value == selected) return;
            selected = Service.ItemSelected(value);
        }
    }

    public void EmptyTo(Node root)
    {
        ClearSelection();
        TreeItems.Clear();
        SelectedPeers.Clear();
        rootItem = TreeItem.CreateTreeItem(this, null, root);
        TreeItems.Add(rootItem);
    }

    public void ClearSelection()
    {
        SelectedItem?.SetIsSelected(false);
        IsSelected = false;
    }

    public void SetSelectedItem(TreeItem item)
    {
        item.SetIsSelected(true);
        IsSelected = true;
    }

    public TreeItem AddNode(Node node)
    {
        // First add Ancestor items, so the node can be added to its parent item
        var parentItem = AddAncestors(node);

        var item = parentItem.AddChildNode(node);
        item.ShowTreeItem();
        return item;
    }

    public bool IsNodeIncluded(Node node) => IsSelected || SelectedPeers.Contains(node.Id);

    public bool HasTreeItemChildren(Node node) => IsSelected && node.Children.Any() || node.Children.Any(n => SelectedPeers.Contains(n.Id));

    // public bool HasNodeChildren(NodeId nodeId) => IsSelected || node.Children.Any(n => SelectedPeers.Contains(n.Id));

    TreeItem AddAncestors(Node node)
    {
        // Start from root, but skip root, since it is already added by default
        var ancestors = node.Ancestors().Reverse().Skip(1);

        var ancestorItem = rootItem;
        foreach (var ancestor in ancestors)
        {   // Add ancestor item if not already added
            var item = ancestorItem.ChildItems.FirstOrDefault(n => n.NodeId == ancestor.Id);
            if (item == null)
            {
                item = ancestorItem.AddChildNode(ancestor);
            }
            ;

            ancestorItem = item!;
        }

        return ancestorItem!;
    }
}
