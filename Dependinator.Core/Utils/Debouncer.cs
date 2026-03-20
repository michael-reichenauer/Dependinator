namespace Dependinator.Core.Utils;

public sealed class Debouncer : IDisposable
{
    private readonly Timer timer;
    private Action? action;
    private readonly object syncRoot = new();
    private DateTime? firstDebounceTimeUtc;
    private DateTime? nextInvokeTimeUtc;

    public Debouncer()
    {
        timer = new Timer(_ => InvokeAction(), null, Timeout.Infinite, Timeout.Infinite);
    }

    public void Debounce(int milliseconds, Action action)
    {
        Debounce(TimeSpan.FromMilliseconds(milliseconds), action);
    }

    public void Debounce(int milliseconds, int maximumDelayMilliseconds, Action action)
    {
        Debounce(TimeSpan.FromMilliseconds(milliseconds), TimeSpan.FromMilliseconds(maximumDelayMilliseconds), action);
    }

    public void Debounce(TimeSpan delay, Action action)
    {
        lock (syncRoot)
        {
            firstDebounceTimeUtc = null;
            nextInvokeTimeUtc = DateTime.UtcNow + delay;
            this.action = action;
            timer.Change(delay, Timeout.InfiniteTimeSpan);
        }
    }

    public void Debounce(TimeSpan delay, TimeSpan maximumDelay, Action action)
    {
        lock (syncRoot)
        {
            DateTime utcNow = DateTime.UtcNow;
            firstDebounceTimeUtc ??= utcNow;
            this.action = action;

            TimeSpan elapsed = utcNow - firstDebounceTimeUtc.Value;
            TimeSpan dueTime = maximumDelay - elapsed;

            if (dueTime > delay)
                dueTime = delay;
            else if (dueTime < TimeSpan.Zero)
                dueTime = TimeSpan.Zero;

            nextInvokeTimeUtc = utcNow + dueTime;
            timer.Change(dueTime, Timeout.InfiniteTimeSpan);
        }
    }

    public void Dispose() => timer.Dispose();

    void InvokeAction()
    {
        Action? todo;
        lock (syncRoot)
        {
            if (action is null || nextInvokeTimeUtc is null)
                return;

            TimeSpan remaining = nextInvokeTimeUtc.Value - DateTime.UtcNow;
            if (remaining > TimeSpan.Zero)
            {
                timer.Change(remaining, Timeout.InfiniteTimeSpan);
                return;
            }

            todo = action;
            action = null;
            firstDebounceTimeUtc = null;
            nextInvokeTimeUtc = null;
        }

        todo?.Invoke();
    }
}
