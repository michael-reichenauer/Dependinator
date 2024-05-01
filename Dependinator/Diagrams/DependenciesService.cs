using MudBlazor;

namespace Dependinator.Diagrams;


public enum TreeSide { Left, Right }

public class TreeItem
{
    public string Title { get; set; }
    public string Icon { get; set; }
    public bool CanExpand => TreeItems.Any();
    public bool IsExpanded { get; set; }
    public HashSet<TreeItem> TreeItems { get; set; } = new();

    public TreeItem(string title, string icon)
    {
        Title = title;
        Icon = icon;
    }
}

public class TreeData
{
    public HashSet<TreeItem> TreeItems { get; set; } = new();
    public TreeItem Selected { get; set; } = null!;
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
class DependenciesService : IDependenciesService
{
    readonly IApplicationEvents applicationEvents;


    TreeData LeftTreeData { get; } = new();
    TreeData RightTreeData { get; } = new();

    public DependenciesService(IApplicationEvents applicationEvents)
    {
        this.applicationEvents = applicationEvents;
    }

    public bool IsShowExplorer { get; private set; }

    public TreeData TreeData(TreeSide side)
    {
        if (side == TreeSide.Left) return LeftTreeData;
        return RightTreeData;
    }


    public async Task<HashSet<TreeItem>> LoadSubTreeAsync(TreeItem parentNode)
    {
        await Task.Delay(500);
        return parentNode.TreeItems;
    }



    public void ShowExplorer()
    {
        Log.Info("ShowExplorer");
        IsShowExplorer = true;

        LeftTreeData.TreeItems.Add(new TreeItem("All Mail", Icons.Material.Filled.Email));
        LeftTreeData.TreeItems.Add(new TreeItem("Trash", Icons.Material.Filled.Delete));
        LeftTreeData.TreeItems.Add(new TreeItem("Categories", Icons.Material.Filled.Label)
        {
            TreeItems =
            [
                new("Social", Icons.Material.Filled.Group),
                new("Updates", Icons.Material.Filled.Info),
                new("Forums", Icons.Material.Filled.QuestionAnswer),
                new("Promotions", Icons.Material.Filled.LocalOffer)
            ]
        });

        LeftTreeData.TreeItems.Add(new TreeItem("History", Icons.Material.Filled.Label));


        RightTreeData.TreeItems.Add(new TreeItem("All Mail", Icons.Material.Filled.Email));
        RightTreeData.TreeItems.Add(new TreeItem("Trash", Icons.Material.Filled.Delete));
        RightTreeData.TreeItems.Add(new TreeItem("Categories", Icons.Material.Filled.Label)
        {
            TreeItems =
            [
                new("Social", Icons.Material.Filled.Group),
                new("Updates", Icons.Material.Filled.Info),
                new("Forums", Icons.Material.Filled.QuestionAnswer),
                new("Promotions", Icons.Material.Filled.LocalOffer)
            ]
        });
        RightTreeData.TreeItems.Add(new TreeItem("History", Icons.Material.Filled.Label));

        applicationEvents.TriggerUIStateChanged();
    }

    public void HideExplorer()
    {
        Log.Info("HideExplorer");
        IsShowExplorer = false;
        LeftTreeData.TreeItems.Clear();
        RightTreeData.TreeItems.Clear();

        applicationEvents.TriggerUIStateChanged();
    }
}

