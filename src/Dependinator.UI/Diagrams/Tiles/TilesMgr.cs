namespace Dependinator.UI.Diagrams.Tiles;

interface ITilesMgr
{
    ITileCache UseTiles();
    void WithTiles(Action<ITileCache> tileCacheAction);
}

[Scoped]
class TilesMgr : ITilesMgr, IDisposable
{
    readonly ITileCache tilesCache;
    readonly IStateMgr stateMgr;
    readonly IApplicationEvents applicationEvents;

    public TilesMgr(IStateMgr stateMgr, IApplicationEvents applicationEvents)
    {
        this.stateMgr = stateMgr;
        this.applicationEvents = applicationEvents;
        tilesCache = new TileCache(stateMgr.Exit);
        applicationEvents.ModelChanged += ClearCache;
    }

    public void Dispose() => applicationEvents.ModelChanged -= ClearCache;

    public ITileCache UseTiles()
    {
        stateMgr.Enter();
        return tilesCache;
    }

    public void WithTiles(Action<ITileCache> tileCacheAction)
    {
        using var tilesCache = UseTiles();
        tileCacheAction(tilesCache);
    }

    void ClearCache()
    {
        using var tilesCache = UseTiles();
        tilesCache.ClearCache();
    }
}
