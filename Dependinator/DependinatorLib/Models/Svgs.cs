namespace Dependinator.Models;


record Level(string Svg, double Zoom);



record Svgs(IReadOnlyList<Level> levels)
{
    public (string, double, int) Get(double zoom)
    {
        if (levels.Count == 0) return ("", 1.0, 0);

        int level = 0;

        for (int i = 1; i < levels.Count; i++)
        {
            if (zoom >= levels[i].Zoom) break;
            level = i;
        }

        return (levels[level].Svg, levels[level].Zoom, level);
    }
}
