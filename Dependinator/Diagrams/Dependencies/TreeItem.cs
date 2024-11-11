using Dependinator.Models;
using MudBlazor;

namespace Dependinator.Diagrams;


internal class TreeItem : TreeItemData<TreeItem>
{
    readonly Lazy<List<TreeItemData<TreeItem>>> hasChildrenItems = new(() =>
        [new TreeItem(null!) { Text = "", Parent = null, Icon = "", NodeId = NodeId.Empty }]);

    readonly Tree tree;
    bool isExpanded;

    public TreeItem(Tree tree)
    {
        this.Value = this;
        this.tree = tree;
    }

    public override string? Text { get; set; }
    public override string? Icon { get; set; }
    public override bool Selected { get; set; }

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

    //  public virtual bool Expandable { get; set; } = true; // Is this needed???
    //  public virtual bool Visible { get; set; } = true; // Is this needed???

    public override List<TreeItemData<TreeItem>>? Children
    {
        get
        {
            if (!IsChildrenIntitialized && HasNodeChildren && ChildItems.Count == 0) return hasChildrenItems.Value;

            return ChildItems.Cast<TreeItemData<TreeItem>>().ToList();
        }
    }


    public Tree Tree => tree;

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


    public bool IsChildrenIntitialized { get; set; }
    public bool IsParentSelected { get; set; }

    public Color TextColor => Selected || IsParentSelected ? Color.Info : Color.Inherit;


    public List<TreeItem> ChildItems = [];


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
        var existingChildItem = FindChildItem(node.Id);
        if (existingChildItem != null) return existingChildItem;

        var newChildItem = CreateTreeItem(Tree, this, node);
        ChildItems.Add(newChildItem);

        if ((node.Parent?.Children?.Count ?? 0) == ChildItems.Count)
        {
            IsChildrenIntitialized = true;
        }

        return newChildItem;
    }

    private TreeItem? FindChildItem(NodeId nodeId) => ChildItems.FirstOrDefault(n => n.Value!.NodeId == nodeId)?.Value;

    public void ShowTreeItem() => this.Ancestors().ForEach(a => a.isExpanded = true);

    public static TreeItem CreateTreeItem(Tree tree, TreeItem? parent, Node node) => new(tree)
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
