using Dependinator.Models;
using MudBlazor;

namespace Dependinator.Diagrams;


public enum TreeSide { Left, Right }

internal class TreeItem(DependenciesService service)
{
    bool isExpanded;

    public string Title { get; set; } = "";
    public string Icon { get; set; } = Icons.Material.Filled.Folder;

    public string ExpandIcon => HasAllItems ? Icons.Material.Filled.ArrowRight :
        isExpanded ? Icons.Material.Filled.ArrowDropUp : Icons.Material.Filled.ArrowRight;


    public bool IsExpanded
    {
        get => isExpanded;

        set
        {
            Log.Info("Set IsExpanded", Title, value);
            if (!HasAllItems)
            {
                service.SetAllItems(this);
                HasAllItems = true;
                if (value) isExpanded = true;
                return;
            }

            isExpanded = value;
        }
    }

    public bool HasAllItems { get; set; }
    public bool IsSelected { get; set; }
    public HashSet<TreeItem> Items { get; set; } = [];


    public TreeItem? Parent { get; set; }
    public required NodeId NodeId { get; init; } = NodeId.Empty;

    public IEnumerable<TreeItem> Ancestors()
    {
        var current = this;
        while (current.Parent != null)
        {
            yield return current.Parent;
            current = current.Parent;
        }
    }

    internal void SetIsExpanded(bool isExpanded) => this.isExpanded = isExpanded;
}

internal class TreeData
{
    TreeItem selected = null!;

    public HashSet<TreeItem> Items { get; set; } = new();
    public TreeItem Selected
    {
        get => selected;
        set
        {
            Log.Info("Selected", value?.Title);
            if (value == null || value == selected) return;
            if (selected != null) selected.IsSelected = false;
            selected = value;
            selected.IsSelected = true;
            //selected.Items.ForEach(item => { item.Items.Clear(); item.IsExpanded = false; });
        }
    }
}




interface IDependenciesService
{
    bool IsShowExplorer { get; }

    TreeData TreeData(TreeSide side);

    Task<HashSet<TreeItem>> LoadSubTreeAsync(TreeItem parentNode);

    void ShowExplorer();
    void HideExplorer();
}


[Scoped]
class DependenciesService(
    ISelectionService selectionService,
    IApplicationEvents applicationEvents,
    IModelService modelService) : IDependenciesService
{
    TreeData left = new();
    TreeData right = new();

    public bool IsShowExplorer { get; private set; }

    public TreeData TreeData(TreeSide side)
    {
        if (side == TreeSide.Left) return left;
        return right;
    }


    public async Task<HashSet<TreeItem>> LoadSubTreeAsync(TreeItem parentNode)
    {
        await Task.CompletedTask;
        return parentNode.Items;
    }



    public void ShowExplorer()
    {
        var selectedId = selectionService.SelectedId.Id;

        left = new();
        right = new();

        using (var model = modelService.UseModel())
        {
            if (!model.TryGetNode(selectedId, out var node)) return;

            var leftRoot = ToItem(model.Root, false);
            var rightRoot = ToItem(model.Root, false);
            left.Items.Add(leftRoot);
            right.Items.Add(rightRoot);

            AddDecendantNode(leftRoot, node);
            node.SourceLinks.ForEach(l => AddDecendantNode(rightRoot, l.Target));

            SelectNode(left, node);
        }

        IsShowExplorer = true;
        applicationEvents.TriggerUIStateChanged();
    }

    internal void SetAllItems(TreeItem treeItem)
    {
        Log.Info("SetAllItems", treeItem.Title);
        using var model = modelService.UseModel();
        if (model.TryGetNode(treeItem.NodeId, out var node))
        {
            treeItem.Items.Clear();
            node.Children.ForEach(child => AddChildNode(treeItem, child, true));
        }
        else
        {
            Log.Info("Node not found", treeItem.NodeId);
        }
    }

    private void SelectNode(TreeData tree, Node node)
    {
        var item = FindItemWithNode(tree.Items.First(), node);
        if (item == null) return;
        Log.Info("SelectNode", item.Title);

        tree.Selected = item;
        item.Ancestors().ForEach(a => a.SetIsExpanded(true));
    }

    public TreeItem? FindItemWithNode(TreeItem root, Node node)
    {
        if (root.NodeId == node.Id) return root;

        return root.Items
            .Select(child => FindItemWithNode(child, node))
            .FirstOrDefault(result => result != null);
    }


    TreeItem AddChildNode(TreeItem parent, Node node, bool addExandable)
    {
        var nodeItem = ToItem(node, addExandable);
        nodeItem.Parent = parent;
        parent.Items.Add(nodeItem);
        return nodeItem;
    }


    void AddDecendantNode(TreeItem rootItem, Node node)
    {
        // Start from root, but skip root
        var ancestors = node.Ancestors().Reverse().Skip(1);
        var current = rootItem;

        foreach (var ancestor in ancestors)
        {
            var ancestorItem = current.Items.FirstOrDefault(n => n.NodeId == ancestor.Id);
            if (ancestorItem != null)
            {   // Ancestor already added
                current = ancestorItem;
                continue;
            }

            // Add ancestor node to tree
            current = AddChildNode(current, ancestor, false);
        }

        // Add node to its parent
        AddChildNode(current, node, true);
    }

    public void HideExplorer()
    {
        Log.Info("HideExplorer");
        IsShowExplorer = false;
        left = new();
        right = new();

        applicationEvents.TriggerUIStateChanged();
    }

    TreeItem ToItem(Node node, bool addExandable)
    {
        var name = node.IsRoot ? "<all>" : node.ShortName;
        var expandableItems = addExandable && node.Children.Any()
            ? [new TreeItem(this) { Title = "...", Icon = @Icons.Material.Filled.Crop32, NodeId = NodeId.Empty }]
            : new HashSet<TreeItem>();
        return new TreeItem(this)
        {
            Title = name,
            Icon = Dependinator.DiagramIcons.Icon.GetIcon(node.Type.Text),
            NodeId = node.Id,
            Items = expandableItems
        };
    }
}