
namespace Dependinator.Utils.UI;

interface IApplicationEvents
{
    event Action UIStateChanged;
    event Action SaveNeeded;

    void TriggerUIStateChanged();
    void TriggerSaveNeeded();
}


[Singleton]
class ApplicationEvents : IApplicationEvents
{
    public event Action UIStateChanged = null!;
    public event Action SaveNeeded = null!;

    public void TriggerUIStateChanged() => UIStateChanged?.Invoke();
    public void TriggerSaveNeeded() => SaveNeeded?.Invoke();
}