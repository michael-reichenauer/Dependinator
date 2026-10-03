using Dependinator.UI.Modeling.Models;
using MudBlazor;

namespace Dependinator.UI.Diagrams.Dependencies;

delegate IReadOnlyList<TreeItemData<TreeItem>> GetTreeItemChildren();

class TreeItem : TreeItemData<TreeItem>
{
    // A single dummy child makes the tree show an expand arrow until the real children
    // are created on first expand.
    static readonly List<TreeItemData<TreeItem>> uninitializedChildren = [new TreeItem()];

    readonly GetTreeItemChildren? getChildren;
    bool isExpanded;
    bool areChildrenCreated = true;

    public TreeItem() { }

    public TreeItem(Node node, int linkCount, int hopCount, GetTreeItemChildren? getChildren)
    {
        Value = this;
        NodeId = node.Id;
        Text = node.ShortName;
        Icon = Icons.Icon.GetIcon(node);
        CanShowEditor = Dependinator.Core.Build.IsVsCodeExtWasm && node.FileSpanOrParentSpan is not null;
        LinkCount = linkCount;
        HopCount = hopCount;
        this.getChildren = getChildren;

        if (getChildren is not null)
        {
            areChildrenCreated = false;
            Children = uninitializedChildren;
        }
    }

    public NodeId NodeId { get; init; } = NodeId.Empty;
    public bool CanShowEditor { get; }

    // Direct links of the subject that this row's lines carry; 0 for a row that exists only
    // because something reached indirectly lies inside or at it.
    public int LinkCount { get; }

    // For a node the subject reaches only through other nodes: the number of hops; 0 otherwise
    // (a direct row, or a container that merely holds indirect ones).
    public int HopCount { get; }

    // Drawn dimmed: no direct link of the subject leads here.
    public bool IsIndirect => LinkCount == 0 && NodeId != NodeId.Empty;
    public override IReadOnlyCollection<ITreeItemData<TreeItem>>? Children { get; set; } = [];

    public IEnumerable<TreeItem> GetThisAndDescendants()
    {
        yield return this;
        if (Children is null)
            yield break;
        foreach (var child in Children.Cast<TreeItem>())
        {
            foreach (var grandChild in child.GetThisAndDescendants())
                yield return grandChild;
        }
    }

    public override bool Expanded
    {
        get => isExpanded;
        set
        {
            isExpanded = value;
            if (isExpanded && !areChildrenCreated)
            {
                // Replace the dummy child with the real children on first expand
                Children = getChildren is null ? [] : [.. getChildren()];
                areChildrenCreated = true;
            }
        }
    }
}
