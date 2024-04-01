
namespace Dependinator.Shared;

interface IApplicationEvents
{
    event Action UIStateChanged;
    event Action SaveNeeded;
    event Action Unselected;

    void TriggerUIStateChanged();
    void TriggerSaveNeeded();
    void TriggerUnselected();
}


[Singleton]
class ApplicationEvents : IApplicationEvents
{
    public event Action UIStateChanged = null!;
    public event Action SaveNeeded = null!;
    public event Action Unselected = null!;

    public void TriggerUIStateChanged() => UIStateChanged?.Invoke();
    public void TriggerSaveNeeded() => SaveNeeded?.Invoke();
    public void TriggerUnselected() => Unselected?.Invoke();
}