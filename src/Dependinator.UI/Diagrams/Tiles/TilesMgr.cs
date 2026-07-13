// Tile cache for the diagram: splits the rendered SVG into cached tiles so panning and zooming
// only regenerate the visible area. Access is guarded by the scoped IStateMgr lock, which is
// shared with ModelMgr, so model and tiles use one reentrant state lock.
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

    void ClearCache() => WithTiles(tiles => tiles.ClearCache());
}
