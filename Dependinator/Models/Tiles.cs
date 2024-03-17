
namespace Dependinator.Models;


record Tile(TileKey Key, string Svg, double Zoom, Pos Offset)
{
    public const double ZoomFactor = 1.1;
    public static Tile Empty = new(TileKey.Empty, "", 1.0, Pos.Zero);
}

record TileKey(long X, long Y, int Z, int TileSize)
{
    public const int XTileSize = 2000;
    public static TileKey Empty = new(0L, 0L, 0, 0);

    public double GetTileZoom() => Math.Pow(Tile.ZoomFactor, -Z);
    public Rect GetTileRect() => new(X * TileSize, Y * TileSize, TileSize, TileSize);
    public Rect GetViewRect() => new(-TileSize * 2, -TileSize * 2, TileSize * 5, TileSize * 5);

    public Rect GetTileRectWithMargin() => new(-TileSize, -TileSize, TileSize * 3, TileSize * 3);

    public static TileKey From(Rect canvasRect, Double canvasZoom)
    {
        var tileSize = (int)Math.Max(canvasRect.Width, canvasRect.Height);
        int z = -(int)Math.Floor(Math.Log(canvasZoom) / Math.Log(Tile.ZoomFactor));
        double tileZoom = Math.Pow(Tile.ZoomFactor, -z);

        long x = (long)Math.Round(canvasRect.X / tileZoom / tileSize);
        long y = (long)Math.Round(canvasRect.Y / tileZoom / tileSize);

        return new TileKey(x, y, z, tileSize);
    }

    public override string ToString() => $"(({X},{Y},{Z}))";
}


class Tiles
{
    readonly Dictionary<TileKey, Tile> tiles = new();
    int tileSize = 0;
    int maxSvgSize = 0;

    public bool TryGetCached(TileKey key, out Tile tile)
    {
        ValidateTileSize(key);
        return tiles.TryGetValue(key, out tile!);
    }

    public void SetCached(Tile tile)
    {
        ValidateTileSize(tile.Key);
        tiles[tile.Key] = tile;
        tileSize = tile.Key.TileSize;

        if (tile.Svg.Length > maxSvgSize)
        {
            maxSvgSize = tile.Svg.Length;
        }
    }

    public void Clear()
    {
        tiles.Clear();
        tileSize = 0;
        maxSvgSize = 0;
    }

    public override string ToString() => $"{tiles.Count} (max: {maxSvgSize})";

    void ValidateTileSize(TileKey key)
    {
        if (tileSize != key.TileSize)
        {   // Tile sizes have been changed, invalidate all tiles.
            Clear();
        }
    }
}
