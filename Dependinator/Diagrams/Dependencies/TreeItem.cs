using Dependinator.Models;
using MudBlazor;

namespace Dependinator.Diagrams;



internal class TreeItem(Tree tree)
{
    readonly HashSet<TreeItem> items = [];
    readonly Lazy<HashSet<TreeItem>> hasChildrenItems = new(() =>
        [new(null!) { Title = "", Parent = null, Icon = "", NodeId = NodeId.Empty }]);

    bool isExpanded;


    public required string Title { get; init; }
    public required string Icon { get; init; }
    public required TreeItem? Parent { get; set; }
    public required NodeId NodeId { get; init; }
    public bool HasNodeChildren { get; internal set; }
    public TreeSide Side => tree.Side;

    // Normally, the expand icon is an right array, which the MudTreeView will rotate to down when expanded
    // But if children are not yet initialized, we need to compensate and thus show an upp arrow
    // which is rotated to right icon if exanded 
    public string ExpandIcon => !IsChildrenIntitialized && isExpanded
        ? Icons.Material.Filled.ArrowDropUp
        : Icons.Material.Filled.ArrowRight;


    public bool IsExpanded
    {
        get => isExpanded;

        set
        {
            if (!IsChildrenIntitialized)
            {
                IntializeChildrenItems();
                if (!value) return;  // Do not collapse if children where not yet initialized
            }

            isExpanded = value;
        }
    }


    public bool IsChildrenIntitialized { get; set; }
    public bool IsSelected { get; set; }
    public bool IsParentSelected { get; set; }

    public Color TextColor => IsSelected || IsParentSelected ? Color.Info : Color.Inherit;


    public HashSet<TreeItem> Items
    {
        get
        {
            if (!IsChildrenIntitialized && HasNodeChildren && items.Count == 0) return hasChildrenItems.Value;

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

        var nodeItem = CreateTreeItem(node, this, tree);
        items.Add(nodeItem);

        if (node.Parent.Children.Count == items.Count)
        {
            IsChildrenIntitialized = true;
        }

        return nodeItem;
    }

    public void ShowTreeItem() => this.Ancestors().ForEach(a => a.isExpanded = true);

    public static TreeItem CreateTreeItem(Node node, TreeItem? parent, Tree tree) => new(tree)
    {
        Title = node.IsRoot ? "<all>" : node.ShortName,
        Icon = Dependinator.DiagramIcons.Icon.GetIcon(node.Type.Text),
        NodeId = node.Id,
        HasNodeChildren = tree.HasNodeChildren(node),
        Parent = parent,
    };

    void IntializeChildrenItems()
    {
        tree.Service.GetChildren(NodeId)
            .Where(tree.IsNodeIncluded)
            .ForEach(child => AddChildNode(child));
        IsChildrenIntitialized = true;
    }

    IEnumerable<TreeItem> Ancestors()
    {
        var current = this;
        while (current.Parent != null)
        {
            yield return current.Parent;
            current = current.Parent;
        }
    }
}
