using Dependinator.Models;
using MudBlazor;

namespace Dependinator.Diagrams.Dependencies;

class TreeItem : TreeItemData<TreeItem>
{
    static readonly List<TreeItemData<TreeItem>> uninitializedChildren = [new TreeItem()];
    private readonly IDependenciesService service;
    private readonly Func<TreeItem, IReadOnlyList<TreeItem>> getChildren;
    bool isExpanded;
    bool areChildrenInitialized = true;

    public IReadOnlyList<TreeItem> ChildItems { get; private set; } = [];

    public TreeItem? Parent { get; init; }
    public NodeId NodeId { get; init; } = NodeId.Empty!;

    public override List<TreeItemData<TreeItem>>? Children => !areChildrenInitialized && !ChildItems.Any()
        ? uninitializedChildren
        : [.. ChildItems];

    public override bool Expanded
    {
        get => isExpanded;
        set
        {
            isExpanded = value;
            if (isExpanded && !areChildrenInitialized)
                InitializeChildrenItems();
        }
    }

    public TreeItem() { }

    public TreeItem(
        IDependenciesService service,
         TreeItem? parent,
         Node node,
         bool hasChildren,
         Func<TreeItem, IReadOnlyList<TreeItem>> getChildren)
    {
        Value = this;
        this.service = service;
        Parent = parent;
        this.getChildren = getChildren;
        NodeId = node.Id;
        areChildrenInitialized = !hasChildren;
    }


    public TreeItem AddChildNode(Node node)
    {
        // Check if node already added
        var existingChildItem = FindChildItem(node.Id);
        if (existingChildItem is not null) return existingChildItem;

        var newChildItem = CreateTreeItem(service, this, node);
        ChildItems.Add(newChildItem);

        if ((node.Parent?.Children?.Count ?? 0) == ChildItems.Count)
        {
            areChildrenInitialized = true;
        }

        return newChildItem;
    }

    private TreeItem? FindChildItem(NodeId nodeId) => ChildItems.FirstOrDefault(n => n.NodeId == nodeId);

    public void ShowTreeItem() => this.Ancestors().ForEach(a => a.isExpanded = true);

    public static TreeItem CreateTreeItem(IDependenciesService service, TreeItem? parent, Node node)
    {
        var hasChildren = node.Children.Any();
        return new(service, parent, node, hasChildren, null!)
        {
            Text = node.IsRoot ? "<all>" : node.ShortName,
            Icon = Dependinator.DiagramIcons.Icon.GetIcon(node.Type.Text),
        };
    }

    void InitializeChildrenItems()
    {
        Log.Info($"InitializeChildrenItems: {NodeId}, ");
        ChildItems = getChildren(this) ?? [];

        // service.GetChildren(NodeId)
        //     .ForEach(child => AddChildNode(child));
        areChildrenInitialized = true;
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
