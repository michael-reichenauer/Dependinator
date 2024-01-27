
namespace Dependinator.Models;


record SvgPart(SvgKey Key, string Svg, double Zoom, Pos Offset);

record SvgKey(int Level)
{
    const double LevelFactor = 2.0;

    public double Zoom() => Math.Pow(LevelFactor, Level);

    public static SvgKey From(Rect rect, Double zoom)
    {
        int level = (int)Math.Floor(Math.Log(zoom) / Math.Log(LevelFactor));
        return new SvgKey(level);
    }
}


class Svgs
{
    private readonly Dictionary<SvgKey, SvgPart> svgs = new();

    public bool TryGet(SvgKey key, out SvgPart svgPart)
    {
        return svgs.TryGetValue(key, out svgPart!);
    }

    public void Set(SvgPart svgPart)
    {
        svgs[svgPart.Key] = svgPart;
    }

    internal void Clear()
    {
        svgs.Clear();
    }
}
