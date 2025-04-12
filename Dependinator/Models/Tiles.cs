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
    int tileWidth = 0;
    int tileHeight = 0;
    int maxSvgSize = 0;
    Rect lastViewRect = Rect.None;
    double lastZoom = 0;
    Tile lastTile = Tile.Empty;

    public bool TryGetLastUsed(Rect viewRect, double zoom, out Tile tile)
    {
        if (viewRect != lastViewRect || zoom != lastZoom)
        {
            lastViewRect = Rect.None;
            lastZoom = 0;
            lastTile = Tile.Empty;
            tile = Tile.Empty;
            return false;
        }
        tile = lastTile;
        return true;
    }

    public bool TryGetCached(TileKey key, Rect viewRect, double zoom, out Tile tile)
    {
        ValidateTileSize(key);
        var isCached = tiles.TryGetValue(key, out tile!);
        if (isCached)
        {
            lastViewRect = viewRect;
            lastZoom = zoom;
            lastTile = tile;
        }

        return isCached;
    }

    public void SetCached(Tile tile, Rect viewRect, double zoom)
    {
        ValidateTileSize(tile.Key);
        tiles[tile.Key] = tile;
        tileWidth = tile.Key.TileWidth;
        tileHeight = tile.Key.TileHeight;
        lastViewRect = viewRect;
        lastZoom = zoom;
        lastTile = tile;

        if (tile.Svg.Length > maxSvgSize)
        {
            maxSvgSize = tile.Svg.Length;
        }
    }

    public void Clear()
    {
        tiles.Clear();
        tileWidth = 0;
        tileHeight = 0;
        maxSvgSize = 0;
        lastViewRect = Rect.None;
        lastZoom = 0;
        lastTile = Tile.Empty;
    }

    public override string ToString() => $"{tiles.Count} (max: {maxSvgSize})";

    void ValidateTileSize(TileKey key)
    {
        if (tileWidth != key.TileWidth || tileHeight != key.TileHeight)
        { // Tile sizes have been changed, invalidate all tiles.
            Clear();
        }
    }
}
