namespace Dependinator.Models;

interface ITilesMgr
{
    ITileCache UseTiles();
    void WithTiles(Action<ITileCache> tileCacheAction);
    void ClearCache();
}

[Scoped]
class TilesMgr(IStateMgr stateMgr) : ITilesMgr
{
    readonly ITileCache tilesCache = new TileCache(stateMgr.Exit);

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

    public void ClearCache()
    {
        using var tilesCache = UseTiles();
        tilesCache.ClearCache();
    }
}
