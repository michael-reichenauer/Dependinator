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
    readonly Action disposeAction;

    readonly Dictionary<TileKey, Tile> tiles = [];
    int currentScreenTileWidth = 0;
    int currentScreenTileHeight = 0;

    Rect lastUsedViewRect = Rect.None;
    double lastUsedZoom = 0;
    Tile lastUsedTile = Tile.Empty;

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

        ClearLastUsed();
        tile = Tile.Empty;
        return false;
    }

    public bool TryGetCached(TileKey key, Rect viewRect, double zoom, out Tile tile)
    {
        ValidateScreenTileSize(key);
        var isCached = tiles.TryGetValue(key, out tile!);
        if (isCached)
        {
            SetLastUsed(viewRect, zoom, tile);
        }

        return isCached;
    }

    public void SetCached(Tile tile, Rect viewRect, double zoom)
    {
        ValidateScreenTileSize(tile.Key);
        tiles[tile.Key] = tile;
        SetCurrentScreenTileSize(tile);
        SetLastUsed(viewRect, zoom, tile);
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

    void ValidateScreenTileSize(TileKey key)
    {
        if (currentScreenTileWidth != key.TileWidth || currentScreenTileHeight != key.TileHeight)
        { // Screen Tile size have been changed, invalidate all cached tiles.
            ClearCache();
        }
    }
}
