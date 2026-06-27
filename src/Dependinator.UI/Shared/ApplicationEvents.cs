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

    /// <summary>
    /// Yields to the browser renderer using requestAnimationFrame.
    /// Best for animation loops where you want to sync to the display refresh rate.
    /// </summary>
    Task YieldAsync();
}

[Scoped]
class ApplicationEvents(IJSInterop jSInterop) : IApplicationEvents
{
    public event Action UIStateChanged = null!;
    public event Action SaveNeeded = null!;
    public event Action UndoneRedone = null!;
    public event Action ModelChanged = null!;

    public void TriggerUIStateChanged() => UIStateChanged?.Invoke();

    public void TriggerSaveNeeded() => SaveNeeded?.Invoke();

    public void TriggerUndoneRedone() => UndoneRedone?.Invoke();

    public void TriggerModelChanged() => ModelChanged?.Invoke();

    public async Task YieldAsync() => await jSInterop.Call("waitForAnimationFrame");
}
