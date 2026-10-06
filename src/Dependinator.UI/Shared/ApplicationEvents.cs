namespace Dependinator.UI.Shared;

// What the user can do about a reported error; the app bar turns it into a snackbar button.
enum ErrorActionKind
{
    RetryLoad,
    RetryRefresh,
    IncludeTestProjects,
}

record ErrorAction(string Label, ErrorActionKind Kind, string? Path = null);

record ErrorReport(string Message, ErrorAction? Action);

interface IApplicationEvents
{
    event Action? UIStateChanged;
    event Action? SaveNeeded;
    event Action? UndoneRedone;
    event Action? ModelChanged;

    // Raised when the viewport (pan offset or zoom) changed: wheel, drag, keyboard, fit, a
    // navigation jump or view history. Not raised for edits; those go through the command
    // stack and ModelChanged.
    event Action? ViewChanged;

    // Raised for failures that services detect but the user must be told about (e.g. a failed
    // parse), optionally with something the user can do about it. Services can be called from
    // background tasks, so the subscribing component is responsible for marshalling to the
    // renderer.
    event Action<ErrorReport>? ErrorReported;

    // Raised for things that happened without the user asking and that they should know about,
    // e.g. what a background refresh changed.
    event Action<string>? InfoReported;

    // Raised when the host (e.g. a VS Code command) asks for the search dialog.
    event Action? SearchRequested;

    void TriggerUIStateChanged();
    void TriggerSaveNeeded();
    void TriggerUndoneRedone();
    void TriggerModelChanged();
    void TriggerViewChanged();
    void TriggerErrorReported(string message, ErrorAction? action = null);
    void TriggerInfoReported(string message);
    void TriggerSearchRequested();

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
    public event Action? ViewChanged;
    public event Action<ErrorReport>? ErrorReported;
    public event Action<string>? InfoReported;
    public event Action? SearchRequested;

    public void TriggerUIStateChanged() => UIStateChanged?.Invoke();

    public void TriggerSaveNeeded() => SaveNeeded?.Invoke();

    public void TriggerUndoneRedone() => UndoneRedone?.Invoke();

    public void TriggerModelChanged() => ModelChanged?.Invoke();

    public void TriggerViewChanged() => ViewChanged?.Invoke();

    public void TriggerErrorReported(string message, ErrorAction? action = null)
    {
        Log.Warn($"Error reported: {message}");
        ErrorReported?.Invoke(new ErrorReport(message, action));
    }

    public void TriggerInfoReported(string message)
    {
        Log.Info($"Info reported: {message}");
        InfoReported?.Invoke(message);
    }

    public void TriggerSearchRequested() => SearchRequested?.Invoke();

    public async Task YieldAsync() => await jSInterop.Call("waitForAnimationFrame");
}
