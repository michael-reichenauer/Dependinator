namespace Dependinator.Utils;

class Disposable : IDisposable
{
    readonly Action action;

    internal Disposable(Action action) => this.action = action;

    public void Dispose() => action?.Invoke();
}
