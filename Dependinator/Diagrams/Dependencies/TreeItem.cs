using Dependinator.Models;
using MudBlazor;

namespace Dependinator.Diagrams;

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
