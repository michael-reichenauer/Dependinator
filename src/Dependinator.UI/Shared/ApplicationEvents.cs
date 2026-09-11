namespace Dependinator.UI.Shared;

interface IApplicationEvents
{
    event Action? UIStateChanged;
    event Action? SaveNeeded;
    event Action? UndoneRedone;
    event Action? ModelChanged;

    // Raised for failures that services detect but the user must be told about (e.g. a failed
    // parse). Services can be called from background tasks, so the subscribing component is
    // responsible for marshalling to the renderer.
    event Action<string>? ErrorReported;

    void TriggerUIStateChanged();
    void TriggerSaveNeeded();
    void TriggerUndoneRedone();
    void TriggerModelChanged();
    void TriggerErrorReported(string message);

    /// <summary>
    /// Yields to the browser renderer using requestAnimationFrame.
    /// Best for animation loops where you want to sync to the display refresh rate.
    /// </summary>
    Task YieldAsync();
}

[Scoped]
class ApplicationEvents(IJSInterop jSInterop) : IApplicationEvents
{
    public event Action? UIStateChanged;
    public event Action? SaveNeeded;
    public event Action? UndoneRedone;
    public event Action? ModelChanged;
    public event Action<string>? ErrorReported;

    public void TriggerUIStateChanged() => UIStateChanged?.Invoke();

    public void TriggerSaveNeeded() => SaveNeeded?.Invoke();

    public void TriggerUndoneRedone() => UndoneRedone?.Invoke();

    public void TriggerModelChanged() => ModelChanged?.Invoke();

    public void TriggerErrorReported(string message)
    {
        Log.Warn($"Error reported: {message}");
        ErrorReported?.Invoke(message);
    }

    public async Task YieldAsync() => await jSInterop.Call("waitForAnimationFrame");
}
