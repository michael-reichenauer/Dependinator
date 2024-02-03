
namespace Dependinator.Models;


record Tile(TileKey Key, string Svg, double Zoom, Pos Offset)
{
    public static Tile Empty = new(TileKey.Empty, "", 1.0, Pos.Zero);
}

record TileKey(int Level, long X, long Y)
{
    public static TileKey Empty = new(0, 0L, 0L);
    const double LevelFactor = 2.0;
    public const long SizeFactor = 10_000L;

    public double Zoom() => Math.Pow(LevelFactor, Level);
    public Rect Rect() => new(X * SizeFactor, Y * SizeFactor, SizeFactor, SizeFactor);

    public static TileKey From(Rect rect, Double zoom)
    {
        int level = (int)Math.Floor(Math.Log(zoom) / Math.Log(LevelFactor));
        double levelZoom = Math.Pow(LevelFactor, level);

        double lx = rect.X / levelZoom;
        double ly = rect.Y / levelZoom;

        int x = (int)Math.Round(lx / SizeFactor);
        int y = (int)Math.Round(ly / SizeFactor);

        // long x = (int)Math.Floor(lx / SizeFactor);
        // long y = (int)Math.Floor(ly / SizeFactor);

        return new TileKey(level, x, y);
    }

    public override string ToString() => $"(({X},{Y}), L: {Level})";
}


class Tiles
{
    readonly Dictionary<TileKey, Tile> tiles = new();

    public bool TryGetCached(TileKey key, out Tile tile) => tiles.TryGetValue(key, out tile!);
    public void SetCached(Tile tile) => tiles[tile.Key] = tile;
    internal void Clear() => tiles.Clear();

    public override string ToString() => $"{tiles.Count}";
}
