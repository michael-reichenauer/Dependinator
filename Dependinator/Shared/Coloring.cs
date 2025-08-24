namespace Dependinator.Shared;

class DColors
{
    public static readonly string CanvasBackground = MudBlazor.Colors.DeepPurple.Lighten5;
    public static readonly string Highlight = MudBlazor.Colors.Blue.Accent1;
    public static readonly string EditNode = MudBlazor.Colors.Yellow.Darken2;
    public static readonly string ToolBarIcon = MudBlazor.Colors.DeepPurple.Lighten5;
    public static readonly string LineColor = "#B388FF";
    public static readonly string EditNodeBack = MudBlazor.Colors.Gray.Darken4;
    //public static readonly string EditNodeBack = new Color(0xFB / EditFactor, 0xC0 / EditFactor, 0x2D / EditFactor).ToString();
}

record Coloring(int R, int G, int B)
{
    const int VeryDarkFactor = 12;
    const int EditFactor = 7;
    const int Bright = 200;
    static readonly Random random = new();

    public static readonly Coloring Zero = new(0, 0, 0);

    public override string ToString() => $"#{R:x2}{G:x2}{B:x2}";

    public static Coloring BrightRandom()
    {
        while (true)
        {
            var (r, g, b) = (random.Next(0, 256), random.Next(0, 256), random.Next(0, 256));
            if (r > Bright || g > Bright || b > Bright)
                return new Coloring(r, g, b);
        }
    }

    public Coloring VeryDark() => new(R / VeryDarkFactor, G / VeryDarkFactor, B / VeryDarkFactor);
}
