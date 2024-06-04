using Dependinator.Models;
using MudBlazor;

namespace Dependinator.Diagrams;



internal class TreeItem
{
    bool isExpanded;
    readonly HashSet<TreeItem> items = [];
    readonly Lazy<HashSet<TreeItem>> emptyItems;

    public readonly Tree Tree;

    public TreeItem(Tree tree)
    {
        this.Tree = tree;
        emptyItems = new Lazy<HashSet<TreeItem>>(() =>
            [new(this.Tree) { Title = "...", Parent = null, Icon = @Icons.Material.Filled.Crop32, NodeId = NodeId.Empty }]);
    }

    public required string Title { get; init; }
    public required string Icon { get; init; }
    public required TreeItem? Parent { get; set; }
    public required NodeId NodeId { get; init; }
    public int NodeChildrenCount { get; internal set; }
    public TreeSide Side => Tree.Side;

    public string ExpandIcon => HasAllItems ? Icons.Material.Filled.ArrowRight :
        isExpanded ? Icons.Material.Filled.ArrowDropUp : Icons.Material.Filled.ArrowRight;


    public bool IsExpanded
    {
        get => isExpanded;

        set
        {
            if (!HasAllItems)
            {
                Tree.Service.SetChildrenItems(this);
                HasAllItems = true;
                if (value) isExpanded = true;
                return;
            }

            if (IsParentSelected && !value) return;  // Dont allow parents of selected to be closed

            isExpanded = value;
        }
    }

    public bool HasAllItems { get; set; }
    public bool IsSelected { get; set; }
    public bool IsParentSelected { get; set; }

    public Color TextColor => IsSelected || IsParentSelected ? Color.Info : Color.Inherit;


    public HashSet<TreeItem> Items
    {
        get
        {
            if (NodeChildrenCount > 0 && items.Count == 0) return emptyItems.Value;

            return items;
        }
    }

    public void SetIsSelected(bool isSelected)
    {
        IsSelected = isSelected;
        Parent?.SetIsParentSelected(isSelected);
    }

    void SetIsParentSelected(bool isSelected)
    {
        IsParentSelected = isSelected;
        Parent?.SetIsParentSelected(isSelected);
    }


    public TreeItem AddChildNode(Node node)
    {
        // Check if node already added
        var item = items.FirstOrDefault(n => n.NodeId == node.Id);
        if (item != null) return item;

        var nodeItem = CreateTreeItem(node, this, Tree);
        items.Add(nodeItem);

        return nodeItem;
    }

    public void ShowTreeItem() => this.Ancestors().ForEach(a => a.isExpanded = true);

    IEnumerable<TreeItem> Ancestors()
    {
        var current = this;
        while (current.Parent != null)
        {
            yield return current.Parent;
            current = current.Parent;
        }
    }


    static TreeItem CreateTreeItem(Node node, TreeItem parent, Tree tree) => new(tree)
    {
        Title = node.ShortName,
        Icon = Dependinator.DiagramIcons.Icon.GetIcon(node.Type.Text),
        NodeId = node.Id,
        NodeChildrenCount = tree.IsSelected ? node.Children.Count :
        node.Children.Count(n => tree.SelectedPeers.Contains(n.Id)),
        Parent = parent,
    };
}
