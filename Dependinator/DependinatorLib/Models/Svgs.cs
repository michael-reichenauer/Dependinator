namespace Dependinator.Models;


record LevelSvg(int Level, string Svg, double Zoom, Pos Offset);



record Svgs(IReadOnlyList<LevelSvg> levels)
{
    public LevelSvg Get(double zoom)
    {
        if (levels.Count == 0) return new LevelSvg(0, "", 1.0, Pos.Zero);

        int level = 0;

        for (int i = 1; i < levels.Count; i++)
        {
            if (zoom >= levels[i].Zoom) break;
            level = i;
        }

        return levels[level];
    }
}
