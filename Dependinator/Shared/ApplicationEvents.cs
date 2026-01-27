namespace Dependinator.Shared;

interface IApplicationEvents
{
    event Action UIStateChanged;
    event Action SaveNeeded;
    event Action UndoneRedone;
    event Action<ExtensionEvent> ExtensionEventOccurred;

    void TriggerUIStateChanged();
    void TriggerSaveNeeded();
    void TriggerUndoneRedone();
    void TriggerExtensionEvent(string type, string message);
}

record ExtensionEvent(string Type, string Message);

[Scoped]
class ApplicationEvents : IApplicationEvents
{
    public event Action UIStateChanged = null!;
    public event Action SaveNeeded = null!;
    public event Action UndoneRedone = null!;
    public event Action<ExtensionEvent> ExtensionEventOccurred = null!;

    public void TriggerUIStateChanged() => UIStateChanged?.Invoke();

    public void TriggerSaveNeeded() => SaveNeeded?.Invoke();

    public void TriggerUndoneRedone() => UndoneRedone?.Invoke();

    public void TriggerExtensionEvent(string type, string message) =>
        ExtensionEventOccurred?.Invoke(new ExtensionEvent(type, message));
}
