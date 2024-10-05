using Dependinator.Models;

namespace Dependinator.Diagrams;


public enum TreeSide { Left, Right }


internal class Tree
{
    MudBlazor.TreeItemData<TreeItem> rootItem;
    MudBlazor.TreeItemData<TreeItem> selected = null!;
    List<MudBlazor.TreeItemData<TreeItem>> Items { get; } = [];

    public Tree(DependenciesService service, TreeSide side, Node root)
    {
        Side = side;
        Service = service;
        rootItem = TreeItem.CreateTreeItem(root, null, this);
        Items.Add(rootItem);
    }


    public TreeSide Side { get; }
    public DependenciesService Service { get; }
    public IReadOnlyCollection<MudBlazor.TreeItemData<TreeItem>> TreeItems => Items;
    public bool IsSelected { get; set; }
    public HashSet<NodeId> SelectedPeers { get; } = [];

    public string Title => IsSelected ? Selected.Text! : "";

    public Tree OtherTree => Side == TreeSide.Left ? Service.TreeData(TreeSide.Right) : Service.TreeData(TreeSide.Left);


    // Set by the UI, when a tree item is selected. When starting to show dialog, null is sometimes set.
    public MudBlazor.TreeItemData<TreeItem> Selected
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
        var parenItem = AddAncestors(node);

        var item = parenItem.Value!.AddChildNode(node);
        item.ShowTreeItem();
        return item;
    }

    public bool IsNodeIncluded(Node node) => IsSelected || SelectedPeers.Contains(node.Id);

    public bool HasNodeChildren(Node node) => IsSelected || node.Children.Any(n => SelectedPeers.Contains(n.Id));

    MudBlazor.TreeItemData<TreeItem> AddAncestors(Node node)
    {
        // Start from root, but skip root
        var ancestors = node.Ancestors().Reverse().Skip(1);
        var ancestorItem = rootItem;
        foreach (var ancestor in ancestors)
        {   // Add ancestor item if not already added
            ancestorItem = ancestorItem!.Children?.FirstOrDefault(n => n.Value!.NodeId == ancestor.Id)!.Value
               ?? ancestorItem.Value!.AddChildNode(ancestor).Value;
        }

        return ancestorItem!;
    }

}
