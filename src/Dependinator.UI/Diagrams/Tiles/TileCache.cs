using Dependinator.UI.Shared.Types;

namespace Dependinator.UI.Diagrams.Tiles;

interface ITileCache : IDisposable
{
    bool TryGetLastUsed(Rect viewRect, double zoom, out Tile tile);
    bool TryGetCached(TileKey key, Rect viewRect, double zoom, out Tile tile);
    void SetCached(Tile tile, Rect viewRect, double zoom);
    void ClearCache();
}

class TileCache : ITileCache
{
    const int MaxCachedTiles = 100; // Cap memory, each tile holds a full svg string
    const int EvictCount = MaxCachedTiles / 4; // Evicted at once, so eviction stays rare

    readonly Action disposeAction;

    readonly Dictionary<TileKey, (Tile Tile, long Used)> tiles = [];
    long accessCounter = 0;
    int currentScreenTileWidth = 0;
    int currentScreenTileHeight = 0;

    Rect lastUsedViewRect = Rect.None;
    double lastUsedZoom = 0;
    Tile lastUsedTile = Tile.Empty;

    // Dispose() releases the state lock acquired by TilesMgr.UseTiles()
    public TileCache(Action disposeAction)
    {
        this.disposeAction = disposeAction;
    }

    public void Dispose() => disposeAction();

    public bool TryGetLastUsed(Rect viewRect, double zoom, out Tile tile)
    {
        if (viewRect == lastUsedViewRect && zoom == lastUsedZoom)
        { // No change, just reuse
            tile = lastUsedTile;
            return true;
        }

        tile = Tile.Empty;
        return false;
    }

    public bool TryGetCached(TileKey key, Rect viewRect, double zoom, out Tile tile)
    {
        InvalidateIfTileSizeChanged(key);
        if (!tiles.TryGetValue(key, out var entry))
        {
            tile = Tile.Empty;
            return false;
        }

        tile = entry.Tile;
        tiles[key] = (tile, ++accessCounter);
        SetLastUsed(viewRect, zoom, tile);
        return true;
    }

    public void SetCached(Tile tile, Rect viewRect, double zoom)
    {
        InvalidateIfTileSizeChanged(tile.Key);
        if (tiles.Count >= MaxCachedTiles)
        {
            EvictLeastRecentlyUsed();
        }

        tiles[tile.Key] = (tile, ++accessCounter);
        SetCurrentScreenTileSize(tile);
        SetLastUsed(viewRect, zoom, tile);
    }

    // Evicts the least recently used quarter, so a long session keeps the tiles around the
    // current view instead of periodically losing the whole cache at once.
    void EvictLeastRecentlyUsed()
    {
        var oldestKeys = tiles.OrderBy(entry => entry.Value.Used).Take(EvictCount).Select(entry => entry.Key).ToList();
        foreach (var key in oldestKeys)
        {
            tiles.Remove(key);
        }
    }

    public void ClearCache()
    {
        tiles.Clear();
        SetCurrentScreenTileSize(Tile.Empty);
        ClearLastUsed();
    }

    void ClearLastUsed()
    {
        lastUsedViewRect = Rect.None;
        lastUsedZoom = 0;
        lastUsedTile = Tile.Empty;
    }

    void SetCurrentScreenTileSize(Tile tile)
    {
        currentScreenTileWidth = tile.Key.TileWidth;
        currentScreenTileHeight = tile.Key.TileHeight;
    }

    void SetLastUsed(Rect viewRect, double zoom, Tile tile)
    {
        lastUsedViewRect = viewRect;
        lastUsedZoom = zoom;
        lastUsedTile = tile;
    }

    public override string ToString() => $"{tiles.Count}";

    void InvalidateIfTileSizeChanged(TileKey key)
    {
        if (currentScreenTileWidth != key.TileWidth || currentScreenTileHeight != key.TileHeight)
        { // Screen Tile size have been changed, invalidate all cached tiles.
            ClearCache();
        }
    }
}
