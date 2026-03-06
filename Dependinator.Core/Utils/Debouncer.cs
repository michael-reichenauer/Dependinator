namespace Dependinator.Core.Utils;

public sealed class Debouncer : IDisposable
{
    private readonly Timer timer;
    private Action? action;
    private readonly object syncRoot = new();
    private DateTime? firstDebounceTimeUtc;

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

            timer.Change(dueTime, Timeout.InfiniteTimeSpan);
        }
    }

    public void Dispose() => timer?.Dispose();

    void InvokeAction()
    {
        Action? todo;
        lock (syncRoot)
        {
            todo = action;
            action = null;
            firstDebounceTimeUtc = null;
        }

        todo?.Invoke();
    }
}
