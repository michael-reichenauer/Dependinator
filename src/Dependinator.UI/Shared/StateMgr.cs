namespace Dependinator.UI.Shared;

// A single lock instance shared per circuit/scope (registered as [Scoped]), deliberately
// injected into both ModelMgr and TilesMgr so model state and tile state are guarded by
// the same lock.
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
