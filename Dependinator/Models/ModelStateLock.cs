namespace Dependinator.Models;

interface IModelStateLock
{
    void Enter();
    void Exit();
    IDisposable Use();
}

[Scoped]
class ModelStateLock : IModelStateLock
{
    readonly object syncRoot = new();

    public void Enter()
    {
        Monitor.Enter(syncRoot);
    }

    public void Exit()
    {
        Monitor.Exit(syncRoot);
    }

    public IDisposable Use()
    {
        Enter();
        return new Releaser(syncRoot);
    }

    sealed class Releaser(object syncRoot) : IDisposable
    {
        bool isDisposed;

        public void Dispose()
        {
            if (isDisposed)
                return;

            isDisposed = true;
            Monitor.Exit(syncRoot);
        }
    }
}
