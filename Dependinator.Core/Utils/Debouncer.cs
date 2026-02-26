namespace Dependinator.Core.Utils;

public class Debouncer
{
    private readonly Timer timer;
    private Action? action;
    private readonly object syncRoot = new();

    public Debouncer()
    {
        timer = new Timer(_ => InvokeAction(), null, Timeout.Infinite, Timeout.Infinite);
    }

    public void Debounce(int milliseconds, Action action)
    {
        lock (syncRoot)
        {
            this.action = action;
            timer.Change(milliseconds, Timeout.Infinite);
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
        }

        todo?.Invoke();
    }
}
