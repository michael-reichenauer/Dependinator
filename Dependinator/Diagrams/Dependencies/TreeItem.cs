using Dependinator.Models;
using MudBlazor;

namespace Dependinator.Diagrams;


internal class TreeItem : TreeItemData<TreeItem>
{
    readonly List<TreeItemData<TreeItem>> items = [];
    readonly Lazy<List<TreeItemData<TreeItem>>> hasChildrenItems = new(() =>
        [new TreeItem(null!) { Text = "", Parent = null, Icon = "", NodeId = NodeId.Empty }]);

    readonly Tree tree;
    bool isExpanded;

    public TreeItem(Tree tree)
    {
        this.tree = tree;
        this.Value = this;
    }

    public Tree Tree => tree;
    public required override string? Text { get; set; }
    public required override string? Icon { get; set; }
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


    public override bool Expanded
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
    public override bool Selected { get; set; }
    public bool IsParentSelected { get; set; }

    public Color TextColor => Selected || IsParentSelected ? Color.Info : Color.Inherit;


    public override List<TreeItemData<TreeItem>> Children
    {
        get
        {
            if (!IsChildrenIntitialized && HasNodeChildren && items.Count == 0) return hasChildrenItems.Value;

            return items;
        }
    }

    public void SetIsSelected(bool isSelected)
    {
        Selected = isSelected;
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
        var item = items.FirstOrDefault(n => n.Value!.NodeId == node.Id);
        if (item != null) return item.Value!;

        var nodeItem = CreateTreeItem(node, this, tree);
        items.Add(nodeItem);

        if ((node.Parent?.Children?.Count ?? 0) == items.Count)
        {
            IsChildrenIntitialized = true;
        }

        return nodeItem;
    }

    public void ShowTreeItem() => this.Ancestors().ForEach(a => a.isExpanded = true);

    public static TreeItem CreateTreeItem(Node node, TreeItem? parent, Tree tree) => new(tree)
    {
        Text = node.IsRoot ? "<all>" : node.ShortName,
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
