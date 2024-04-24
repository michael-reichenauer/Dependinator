namespace Dependinator.Diagrams;


interface IDependenciesService
{
    bool IsShowExplorer { get; }

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

    public void ShowExplorer()
    {
        Log.Info("ShowExplorer");
        IsShowExplorer = true;
        applicationEvents.TriggerUIStateChanged();
    }

    public void HideExplorer()
    {
        Log.Info("HideExplorer");
        IsShowExplorer = false;
        applicationEvents.TriggerUIStateChanged();
    }
}

