using Dependinator.Models;
using MudBlazor;

namespace Dependinator.Diagrams.Dependencies;

class TreeItem2 : TreeItemData<TreeItem2>
{
    static readonly List<TreeItemData<TreeItem2>> hasUnitializedChildrenItems =
        [new TreeItem2(null!, null, null) { Text = "xx", Icon = "" }];

    readonly Tree2 tree;
    bool isExpanded;
    bool HasTreeItemChildren;
    bool IsChildrenIntitialized;

    public TreeItem2(Tree2 tree, TreeItem2? parent, Node? node)
    {
        this.tree = tree;
        Parent = parent;
        if (node == null)
        {
            NodeId = NodeId.Empty;
            HasTreeItemChildren = false;
            IsChildrenIntitialized = true;
            return;
        }

        NodeId = node.Id;
        HasTreeItemChildren = tree.HasTreeItemChildren(node);
        IsChildrenIntitialized = !HasTreeItemChildren;
    }


    public List<TreeItem2> ChildItems { get; private set; } = [];

    public bool IsSelected { get; private set; }
    public bool IsChildSelected { get; private set; }

    public override string? Text { get; set; }
    public override string? Icon { get; set; }
    public Tree2 Tree => tree;

    public TreeItem2? Parent { get; init; }
    public NodeId NodeId { get; init; }
    public TreeSide Side => tree.Side;

    public override List<TreeItemData<TreeItem2>>? Children => !IsChildrenIntitialized && !ChildItems.Any()
        ? hasUnitializedChildrenItems
        : [.. ChildItems];

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


    public void ItemClicked() => tree.SelectedItem = this;


    // Normally, the expand icon is an right array, which the MudTreeView will rotate to down when expanded
    // But if children are not yet initialized, we need to compensate and thus show an upp arrow
    // which is rotated to right icon if exanded 
    public string ExpandIcon => !IsChildrenIntitialized && isExpanded
        ? Icons.Material.Filled.ArrowDropUp
        : Icons.Material.Filled.ArrowRight;


    public bool IsParentSelected { get; set; }

    public Color TextColor => Selected || IsParentSelected ? Color.Info : Color.Inherit;


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


    public TreeItem2 AddChildNode(Node node)
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

    private TreeItem2? FindChildItem(NodeId nodeId) => ChildItems.FirstOrDefault(n => n.NodeId == nodeId);

    public void ShowTreeItem() => this.Ancestors().ForEach(a => a.isExpanded = true);

    public static TreeItem2 CreateTreeItem(Tree2 tree, TreeItem2? parent, Node node) => new(tree, parent, node)
    {
        Text = node.IsRoot ? "<all>" : node.ShortName,
        Icon = Dependinator.DiagramIcons.Icon.GetIcon(node.Type.Text),
    };

    void IntializeChildrenItems()
    {
        Log.Info($"IntializeChildrenItems: {NodeId}, ");
        ChildItems = [];
        tree.Service.GetChildren(NodeId)
            .Where(tree.IsNodeIncluded)
            .ForEach(child => AddChildNode(child));
        IsChildrenIntitialized = true;
    }

    IEnumerable<TreeItem2> Ancestors()
    {
        var current = this;
        while (current.Parent != null)
        {
            yield return current.Parent;
            current = current.Parent;
        }
    }
}
