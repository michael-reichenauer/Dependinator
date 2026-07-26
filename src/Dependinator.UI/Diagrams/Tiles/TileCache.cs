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
    // Cap memory, each tile holds a full svg string. Tile sizes vary hugely (a few KB at far
    // zoom to a few hundred KB in a dense view), so a byte budget bounds memory far better than
    // a tile count does. One cache exists per scope, i.e. per Blazor circuit or browser tab.
    const long DefaultMaxCachedBytes = 20 * 1024 * 1024;

    // Secondary guard: a far zoom session produces small tiles, and eviction sorts the whole
    // dictionary, so bound the number of entries as well.
    const int MaxCachedTiles = 500;
    const int EvictToTiles = MaxCachedTiles * 3 / 4;

    readonly Action disposeAction;
    readonly long maxCachedBytes;
    readonly long evictToBytes; // Evicted down to this, so eviction stays rare

    readonly Dictionary<TileKey, (Tile Tile, long Used)> tiles = [];
    long cachedBytes = 0;
    long accessCounter = 0;
    int currentScreenTileWidth = 0;
    int currentScreenTileHeight = 0;

    Rect lastUsedViewRect = Rect.None;
    double lastUsedZoom = 0;
    Tile lastUsedTile = Tile.Empty;

    // Dispose() releases the state lock acquired by TilesMgr.UseTiles(). The budget is only
    // specified by tests, which use a small budget to avoid allocating megabytes of svg.
    public TileCache(Action disposeAction, long maxCachedBytes = DefaultMaxCachedBytes)
    {
        this.disposeAction = disposeAction;
        this.maxCachedBytes = maxCachedBytes;
        this.evictToBytes = maxCachedBytes * 3 / 4;
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
        InvalidateIfTileSizeChanged(tile.Key); // May clear the cache, resetting cachedBytes

        if (tiles.TryGetValue(tile.Key, out var replaced))
        {
            cachedBytes -= replaced.Tile.ByteSize; // Replacing this key, not adding to it
        }

        tiles[tile.Key] = (tile, ++accessCounter);
        cachedBytes += tile.ByteSize;

        // Evicting after the insert keeps the new tile safe: it is the most recently used, so
        // it is the last one eviction would drop, even when it alone exceeds the whole budget.
        EvictUntilWithinBudget();
        SetCurrentScreenTileSize(tile);
        SetLastUsed(viewRect, zoom, tile);
    }

    // Evicts the least recently used tiles down to the low water marks, so a long session keeps
    // the tiles around the current view instead of periodically losing the whole cache at once.
    // Always keeps one tile: a tile larger than the whole budget must still be cached, or the
    // view would re-render it on every frame.
    void EvictUntilWithinBudget()
    {
        if (cachedBytes <= maxCachedBytes && tiles.Count <= MaxCachedTiles)
            return;

        var previousCount = tiles.Count;
        var oldestEntries = tiles.OrderBy(entry => entry.Value.Used).ToList();
        foreach (var entry in oldestEntries)
        {
            if (tiles.Count <= 1)
                break;
            if (cachedBytes <= evictToBytes && tiles.Count <= EvictToTiles)
                break;

            tiles.Remove(entry.Key);
            cachedBytes -= entry.Value.Tile.ByteSize;
        }

        // Rare by construction (the low water mark leaves headroom), so this does not flood the
        // log while panning, and it is what the budget can be re-tuned from later.
        Log.Info($"Evicted {previousCount - tiles.Count} tiles, cache: {this}");
    }

    public void ClearCache()
    {
        tiles.Clear();
        cachedBytes = 0;
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

    public override string ToString() => $"{tiles.Count} tiles, {cachedBytes / 1024} KB";

    void InvalidateIfTileSizeChanged(TileKey key)
    {
        if (currentScreenTileWidth != key.TileWidth || currentScreenTileHeight != key.TileHeight)
        { // Screen Tile size have been changed, invalidate all cached tiles.
            ClearCache();
        }
    }
}
