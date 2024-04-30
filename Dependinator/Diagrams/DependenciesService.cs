using MudBlazor;

namespace Dependinator.Diagrams;


public class TreeItem
{
    public string Title { get; set; }
    public string Icon { get; set; }
    public bool CanExpand => TreeItems.Any();
    public HashSet<TreeItem> TreeItems { get; set; } = new();

    public TreeItem(string title, string icon)
    {
        Title = title;
        Icon = icon;
    }
}

interface IDependenciesService
{
    bool IsShowExplorer { get; }

    TreeItem LeftSelected { get; set; }
    HashSet<TreeItem> LeftTreeItems { get; }
    Task<HashSet<TreeItem>> LeftLoad(TreeItem parentNode);

    TreeItem RightSelected { get; set; }
    HashSet<TreeItem> RightTreeItems { get; }
    Task<HashSet<TreeItem>> RightLoad(TreeItem parentNode);

    void ShowExplorer();
    void HideExplorer();
}


[Scoped]
class DependenciesService : IDependenciesService
{
    readonly IApplicationEvents applicationEvents;

    public DependenciesService(IApplicationEvents applicationEvents)
    {
        this.applicationEvents = applicationEvents;
    }

    public bool IsShowExplorer { get; private set; }

    public HashSet<TreeItem> LeftTreeItems { get; } = new();
    public TreeItem LeftSelected { get; set; } = null!;


    public async Task<HashSet<TreeItem>> LeftLoad(TreeItem parentNode)
    {
        await Task.Delay(500);
        return parentNode.TreeItems;
    }

    public HashSet<TreeItem> RightTreeItems { get; } = new();
    public TreeItem RightSelected { get; set; } = null!;


    public async Task<HashSet<TreeItem>> RightLoad(TreeItem parentNode)
    {
        await Task.Delay(500);
        return parentNode.TreeItems;
    }



    public void ShowExplorer()
    {
        Log.Info("ShowExplorer");
        IsShowExplorer = true;

        LeftTreeItems.Add(new TreeItem("All Mail", Icons.Material.Filled.Email));
        LeftTreeItems.Add(new TreeItem("Trash", Icons.Material.Filled.Delete));
        LeftTreeItems.Add(new TreeItem("Categories", Icons.Material.Filled.Label)
        {
            TreeItems =
            [
                new("Social", Icons.Material.Filled.Group),
                new("Updates", Icons.Material.Filled.Info),
                new("Forums", Icons.Material.Filled.QuestionAnswer),
                new("Promotions", Icons.Material.Filled.LocalOffer)
            ]
        });
        LeftTreeItems.Add(new TreeItem("History", Icons.Material.Filled.Label));

        RightTreeItems.Add(new TreeItem("All Mail", Icons.Material.Filled.Email));
        RightTreeItems.Add(new TreeItem("Trash", Icons.Material.Filled.Delete));
        RightTreeItems.Add(new TreeItem("Categories", Icons.Material.Filled.Label)
        {
            TreeItems =
            [
                new("Social", Icons.Material.Filled.Group),
                new("Updates", Icons.Material.Filled.Info),
                new("Forums", Icons.Material.Filled.QuestionAnswer),
                new("Promotions", Icons.Material.Filled.LocalOffer)
            ]
        });
        RightTreeItems.Add(new TreeItem("History", Icons.Material.Filled.Label));

        applicationEvents.TriggerUIStateChanged();
    }

    public void HideExplorer()
    {
        Log.Info("HideExplorer");
        IsShowExplorer = false;
        LeftTreeItems.Clear();

        applicationEvents.TriggerUIStateChanged();
    }
}

