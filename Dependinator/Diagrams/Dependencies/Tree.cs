using Dependinator.Models;

namespace Dependinator.Diagrams;


public enum TreeSide { Left, Right }


internal class Tree
{
    TreeItem rootItem;
    TreeItem selected = null!;
    List<TreeItem> Items { get; } = [];

    public Tree(DependenciesService service, TreeSide side, Node root)
    {
        Side = side;
        Service = service;
        rootItem = TreeItem.CreateTreeItem(this, null, root);
        Items.Add(rootItem);
    }


    public TreeSide Side { get; }
    public DependenciesService Service { get; }
    public List<TreeItem> TreeItems => Items;
    public bool IsSelected { get; set; }
    public HashSet<NodeId> SelectedPeers { get; } = [];

    public string Title => IsSelected ? Selected?.Text! ?? "" : "";

    public Tree OtherTree => Side == TreeSide.Left ? Service.TreeData(TreeSide.Right) : Service.TreeData(TreeSide.Left);


    // Set by the UI, when a tree item is selected. When starting to show dialog, null is sometimes set.
    public TreeItem Selected
    {
        get => null!;
        set
        {
            if (value == null || value == selected) return;
            selected = value;
            //selected = Service.ItemSelected(value);
        }
    }

    public void EmptyTo(Node root)
    {
        ClearSelection();
        Items.Clear();
        SelectedPeers.Clear();
        rootItem = TreeItem.CreateTreeItem(this, null, root);
        Items.Add(rootItem);
    }

    public void ClearSelection()
    {
        Selected?.Value!.SetIsSelected(false);
        IsSelected = false;
    }

    public void SetSelectedItem(MudBlazor.TreeItemData<TreeItem> item)
    {
        item.Value!.SetIsSelected(true);
        IsSelected = true;
    }

    public TreeItem AddNode(Node node)
    {
        // First add Ancestor items, so the node can be added to its parent item
        var parentItem = AddAncestors(node);

        var item = parentItem.Value!.AddChildNode(node);
        item.ShowTreeItem();
        return item;
    }

    public bool IsNodeIncluded(Node node) => IsSelected || SelectedPeers.Contains(node.Id);

    public bool HasNodeChildren(Node node) => IsSelected || node.Children.Any(n => SelectedPeers.Contains(n.Id));

    TreeItem AddAncestors(Node node)
    {
        // Start from root, but skip root, since it is already added by default
        var ancestors = node.Ancestors().Reverse().Skip(1);

        var ancestorItem = rootItem;
        foreach (var ancestor in ancestors)
        {   // Add ancestor item if not already added
            var item = ancestorItem.ChildItems.FirstOrDefault(n => n.Value!.NodeId == ancestor.Id);
            if (item == null)
            {
                item = ancestorItem.Value!.AddChildNode(ancestor).Value;
            };

            ancestorItem = item!;
        }

        return ancestorItem!;
    }
}
