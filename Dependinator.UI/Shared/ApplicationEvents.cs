namespace Dependinator.UI.Shared;

interface IApplicationEvents
{
    event Action UIStateChanged;
    event Action SaveNeeded;
    event Action UndoneRedone;
    event Action ModelChanged;

    void TriggerUIStateChanged();
    void TriggerSaveNeeded();
    void TriggerUndoneRedone();
    void TriggerModelChanged();
}

[Scoped]
class ApplicationEvents : IApplicationEvents
{
    public event Action UIStateChanged = null!;
    public event Action SaveNeeded = null!;
    public event Action UndoneRedone = null!;
    public event Action ModelChanged = null!;

    public void TriggerUIStateChanged() => UIStateChanged?.Invoke();

    public void TriggerSaveNeeded() => SaveNeeded?.Invoke();

    public void TriggerUndoneRedone() => UndoneRedone?.Invoke();

    public void TriggerModelChanged() => ModelChanged?.Invoke();
}
