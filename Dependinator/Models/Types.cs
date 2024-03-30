namespace Dependinator.Models;

interface IItem
{
}

record Source(string Path, string Text, int LineNumber);


record Pos(double X, double Y)
{
    public static readonly Pos None = new(0, 0);
    public static readonly Pos Zero = None;
    public override string ToString() => $"({X:0.##},{Y:0.##})";
}

record Size(double Width, double Height)
{
    public static readonly Size None = new(0, 0);
    public override string ToString() => $"({Width:0.##},{Height:0.##})";
}

record Rect(double X, double Y, double Width, double Height)
{
    public static readonly Rect None = new(0, 0, 0, 0);
    public override string ToString() => $"({X:0.##},{Y:0.##},{Width:0.##},{Height:0.##})";

    public bool IsPosInside(Pos pos) => pos.X >= X && pos.X <= X + Width && pos.Y >= Y && pos.Y <= Y + Height;
}


record Color(int R, int G, int B)
{
    public static readonly string Highlight = MudBlazor.Colors.DeepPurple.Accent1;
    public static readonly string EditNode = MudBlazor.Colors.Yellow.Darken2;
    public static readonly string ToolBarIcon = MudBlazor.Colors.DeepPurple.Lighten5;


    const int VeryDarkFactor = 12;
    const int EditFactor = 7;
    const int Bright = 200;
    static readonly Random random = new();

    public static readonly Color Zero = new(0, 0, 0);
    public override string ToString() => $"#{R:x2}{G:x2}{B:x2}";

    public static readonly string EditNodeBack = new Color(0xFB / EditFactor, 0xC0 / EditFactor, 0x2D / EditFactor).ToString();

    public static Color BrightRandom()
    {
        while (true)
        {
            var (r, g, b) = (random.Next(0, 256), random.Next(0, 256), random.Next(0, 256));
            if (r > Bright || g > Bright || b > Bright) return new Color(r, g, b);
        }
    }

    public Color VeryDark() => new(R / VeryDarkFactor, G / VeryDarkFactor, B / VeryDarkFactor);
}
