using Dependinator.Models;

namespace Dependinator.Diagrams;


public enum TreeSide { Left, Right }


internal class Tree
{
    TreeItem rootItem;
    TreeItem selected = null!;

    public Tree(DependenciesService service, TreeSide side, Node root)
    {
        Side = side;
        Service = service;
        rootItem = TreeItem.CreateTreeItem(root, null, this);
        Items.Add(rootItem);
    }


    public TreeSide Side { get; }
    public DependenciesService Service { get; }
    public HashSet<TreeItem> Items { get; } = [];
    public bool IsSelected { get; set; }
    public HashSet<NodeId> SelectedPeers { get; } = [];

    public string Title => IsSelected ? Selected.Title : "<all>";

    public Tree OtherTree => Side == TreeSide.Left ? Service.TreeData(TreeSide.Right) : Service.TreeData(TreeSide.Left);


    // Set by the UI, when a tree item is selected. When starting to show dialog, null is sometimes set.
    public TreeItem Selected
    {
        get => selected;
        set
        {
            if (value == null || value == selected) return;

            selected = Service.ItemSelected(value);
        }
    }

    public void EmptyTo(Node root)
    {
        ClearSelection();
        Items.Clear();
        SelectedPeers.Clear();
        rootItem = TreeItem.CreateTreeItem(root, null, this);
        Items.Add(rootItem);
    }

    public void ClearSelection()
    {
        Selected?.SetIsSelected(false);
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
        var parenItem = AddAncestors(node);

        var item = parenItem.AddChildNode(node);
        item.ShowTreeItem();
        return item;
    }

    public bool IsNodeIncluded(Node node) => IsSelected || SelectedPeers.Contains(node.Id);

    public bool HasNodeChildren(Node node) => IsSelected || node.Children.Any(n => SelectedPeers.Contains(n.Id));

    TreeItem AddAncestors(Node node)
    {
        // Start from root, but skip root
        var ancestors = node.Ancestors().Reverse().Skip(1);
        var ancestorItem = rootItem;
        foreach (var ancestor in ancestors)
        {   // Add ancestor item if not already added
            ancestorItem = ancestorItem.Items.FirstOrDefault(n => n.NodeId == ancestor.Id)
               ?? ancestorItem.AddChildNode(ancestor);
        }

        return ancestorItem;
    }

}
