namespace Dependinator.Shared;

interface IApplicationEvents
{
    event Action UIStateChanged;
    event Action SaveNeeded;
    event Action UndoneRedone;

    void TriggerUIStateChanged();
    void TriggerSaveNeeded();
    void TriggerUndoneRedone();
}

[Scoped]
class ApplicationEvents : IApplicationEvents
{
    public event Action UIStateChanged = null!;
    public event Action SaveNeeded = null!;
    public event Action UndoneRedone = null!;

    public void TriggerUIStateChanged() => UIStateChanged?.Invoke();

    public void TriggerSaveNeeded() => SaveNeeded?.Invoke();

    public void TriggerUndoneRedone() => UndoneRedone?.Invoke();
}
