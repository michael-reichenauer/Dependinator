namespace Dependinator.Models;

record Tile(TileKey Key, string Svg, double Zoom, Pos Offset)
{
    public const double ZoomFactor = 1.1; // How often is a new tile needed when zooming
    public static Tile Empty = new(TileKey.Empty, "", 1.0, Pos.None);
}

record TileKey(long X, long Y, int Z, int TileWidth, int TileHeight)
{
    private const double Margin = 0.6; // How much larger than screen needs to be included in tile
    public static TileKey Empty = new(0L, 0L, 0, 0, 0);

    public double GetTileZoom() => Math.Pow(Tile.ZoomFactor, -Z);

    public Rect GetTileRect() => new(X * TileWidth, Y * TileHeight, TileWidth, TileHeight);

    public Rect GetViewRect() => new(-TileWidth * 2, -TileHeight * 2, TileWidth * 5, TileHeight * 5);

    public Rect GetTileRectWithMargin() =>
        new(
            -TileWidth * Margin,
            -TileHeight * Margin,
            TileWidth + 2 * (TileWidth * Margin),
            TileHeight + 2 * (TileHeight * Margin)
        );

    public static TileKey From(Rect canvasRect, Double canvasZoom)
    {
        var tileWidth = (int)Math.Ceiling(canvasRect.Width);
        var tileHeight = (int)Math.Ceiling(canvasRect.Height);

        int z = -(int)Math.Floor(Math.Log(canvasZoom) / Math.Log(Tile.ZoomFactor));
        double tileZoom = Math.Pow(Tile.ZoomFactor, -z);

        long x = (long)Math.Round(canvasRect.X / tileZoom / tileWidth);
        long y = (long)Math.Round(canvasRect.Y / tileZoom / tileHeight);

        return new TileKey(x, y, z, tileWidth, tileHeight);
    }

    public override string ToString() => $"(({X},{Y},{Z}))";
}

class Tiles
{
    readonly Dictionary<TileKey, Tile> tiles = [];
    int currentScreenTileWidth = 0;
    int currentScreenTileHeight = 0;

    Rect lastUsedViewRect = Rect.None;
    double lastUsedZoom = 0;
    Tile lastUsedTile = Tile.Empty;

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
