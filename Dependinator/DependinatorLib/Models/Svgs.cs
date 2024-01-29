
namespace Dependinator.Models;


record SvgTile(SvgKey Key, string Svg, double Zoom, Pos Offset)
{
    public static SvgTile Empty => new(new SvgKey(0, 0, 0), "", 1.0, Pos.Zero);
}

record SvgKey(int Level, int X, int Y)
{
    const double LevelFactor = 2.0;
    public const int SizeFactor = 10_000;

    public double Zoom() => Math.Pow(LevelFactor, Level);
    public Rect Rect() => new(X * SizeFactor, Y * SizeFactor, SizeFactor, SizeFactor);

    public static SvgKey From(Rect rect, Double zoom)
    {
        int level = (int)Math.Floor(Math.Log(zoom) / Math.Log(LevelFactor));
        double levelZoom = Math.Pow(LevelFactor, level);

        var lx = (rect.X) / levelZoom;
        var ly = (rect.Y) / levelZoom;

        int x = (int)Math.Round(lx / SizeFactor);
        int y = (int)Math.Round(ly / SizeFactor);
        return new SvgKey(level, x, y);
    }

    public override string ToString() => $"(({X},{Y}), L: {Level}, Z:{Zoom()}, {Rect()})";
}


class Svgs
{
    private readonly Dictionary<SvgKey, SvgTile> svgs = new();

    public bool TryGetCached(SvgKey key, out SvgTile tile)
    {
        return svgs.TryGetValue(key, out tile!);
    }

    public void SetCached(SvgTile tile)
    {
        svgs[tile.Key] = tile;
    }

    internal void Clear()
    {
        svgs.Clear();
    }

    public override string ToString() => $"{svgs.Count}";
}
