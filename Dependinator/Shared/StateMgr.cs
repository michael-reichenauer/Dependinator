namespace Dependinator.Shared;

interface IStateMgr
{
    void Enter();
    void Exit();
}

[Scoped]
class StateMgr : IStateMgr
{
    readonly object syncRoot = new();

    public void Enter() => Monitor.Enter(syncRoot);

    public void Exit() => Monitor.Exit(syncRoot);
}
