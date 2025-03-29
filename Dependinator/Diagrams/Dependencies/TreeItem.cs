using Dependinator.Models;
using MudBlazor;

namespace Dependinator.Diagrams.Dependencies;

class TreeItem : TreeItemData<TreeItem>
{
    static readonly TreeItemData<TreeItem> uninitializedChild = new TreeItem(null!, null, null, false);
    static readonly List<TreeItemData<TreeItem>> unitializedChildren = [uninitializedChild];

    bool isExpanded;
    bool HasTreeItemChildren;
    bool IsChildrenInitialized;


    public List<TreeItem> ChildItems { get; private set; } = [];

    public bool IsSelected { get; private set; }
    public bool isChildSelected { get; private set; }

    public override string? Text { get; set; }
    public override string? Icon { get; set; }
    public Tree Tree { get; private set; } = null!;

    public TreeItem? Parent { get; init; }
    public NodeId NodeId { get; init; }

    public override List<TreeItemData<TreeItem>>? Children => !IsChildrenInitialized && !ChildItems.Any()
        ? unitializedChildren
        : [.. ChildItems];

    public override bool Expanded
    {
        get => isExpanded;
        set
        {
            if (!IsChildrenInitialized)
            {
                InitializeChildrenItems();
                if (!value) return;  // Do not collapse if children where not yet initialized
            }

            isExpanded = value;
        }
    }

    public TreeItem(Tree tree, TreeItem? parent, Node? node, bool hasChildren)
    {
        Tree = tree;
        Parent = parent;
        if (node == null)
        {
            NodeId = NodeId.Empty;
            HasTreeItemChildren = false;
            IsChildrenInitialized = true;
            return;
        }

        NodeId = node.Id;
        HasTreeItemChildren = hasChildren; //tree.HasTreeItemChildren(node);
        IsChildrenInitialized = !HasTreeItemChildren;
    }

    public void ItemClicked() => Tree.SelectedItem = this;



    public void SetIsSelected(bool isSelected)
    {
        Selected = isSelected;
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
            IsChildrenInitialized = true;
        }

        return newChildItem;
    }

    private TreeItem? FindChildItem(NodeId nodeId) => ChildItems.FirstOrDefault(n => n.NodeId == nodeId);

    public void ShowTreeItem() => this.Ancestors().ForEach(a => a.isExpanded = true);

    public static TreeItem CreateTreeItem(Tree tree, TreeItem? parent, Node node)
    {
        var hasChildren = node.Children.Any();
        return new(tree, parent, node, hasChildren)
        {
            Text = node.IsRoot ? "<all>" : node.ShortName,
            Icon = Dependinator.DiagramIcons.Icon.GetIcon(node.Type.Text),
        };
    }

    void InitializeChildrenItems()
    {
        Log.Info($"IntializeChildrenItems: {NodeId}, ");
        ChildItems = [];
        Tree.Service.GetChildren(NodeId)
            .Where(Tree.IsNodeIncluded)
            .ForEach(child => AddChildNode(child));
        IsChildrenInitialized = true;
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
