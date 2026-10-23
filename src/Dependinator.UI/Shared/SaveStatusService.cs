namespace Dependinator.UI.Shared;

interface ISaveStatusService
{
    // Whether a save of the model or its view is waiting for its debounce or still being written.
    void SetPending(bool isPending);
}

// Marks <body data-save-pending> while a save is debounced or in flight, so UI/e2e tests can
// wait for the model to be stored (e.g. before a reload) instead of sleeping. Only state changes
// reach the browser.
[Scoped]
class SaveStatusService(IJSInterop jsInterop) : ISaveStatusService
{
    readonly object syncRoot = new();
    bool? shownPending; // null when the browser's state is unknown (a call failed)
    Task lastCall = Task.CompletedTask;

    public void SetPending(bool isPending)
    {
        lock (syncRoot)
        {
            if (isPending == shownPending)
                return;
            shownPending = isPending;
            // Chained, so the browser sees the changes in the order they happened
            lastCall = CallAfterAsync(lastCall, isPending);
        }
    }

    async Task CallAfterAsync(Task previous, bool isPending)
    {
        await previous;
        try
        {
            await jsInterop.Call("setSavePending", isPending);
        }
        catch (Exception e)
        {
            Log.Warn($"Failed to set save pending: {e.Message}");
            // Resend on the next change rather than leave the browser showing a stale state
            lock (syncRoot)
                shownPending = null;
        }
    }
}
