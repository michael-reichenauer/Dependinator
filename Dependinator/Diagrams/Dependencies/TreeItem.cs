using Dependinator.Models;
using MudBlazor;

namespace Dependinator.Diagrams.Dependencies;

delegate IReadOnlyList<TreeItemData<TreeItem>> GetTreeItemChildren(TreeItem item);

class TreeItem : TreeItemData<TreeItem>
{
    // To make it look like there are children we use a children item array with one dummy item until first expand
    // Which will then create the actually list of
    static readonly List<TreeItemData<TreeItem>> uninitializedChildren = [new TreeItem()];

    readonly GetTreeItemChildren? getChildren = null!;
    bool isExpanded;
    bool isInitialized = true;

    public TreeItem() { }

    public TreeItem(Node node, TreeItem? parent, GetTreeItemChildren? getChildren)
    {
        Value = this;
        Parent = parent;
        NodeId = node.Id;
        Text = node.ShortName;
        Icon = Dependinator.DiagramIcons.Icon.GetIcon(node.Icon);
        this.getChildren = getChildren;

        if (getChildren is not null)
        {
            // There children to get, but will be retrieved on first expand
            isInitialized = false;
            Children = uninitializedChildren;
        }
    }

    public TreeItem? Parent { get; init; }
    public NodeId NodeId { get; init; } = NodeId.Empty!;
    public override List<TreeItemData<TreeItem>>? Children { get; set; } = [];

    public override bool Expanded
    {
        get => isExpanded;
        set
        {
            isExpanded = value;
            if (isExpanded && !isInitialized)
            {
                // Get the children on first expand
                Children = getChildren is null ? [] : [.. getChildren(this)];
                isInitialized = true;
            }
        }
    }
}
