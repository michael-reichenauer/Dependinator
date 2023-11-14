namespace Dependinator.Models;

record Pos(double X, double Y)
{
    public static readonly Pos Zero = new(0, 0);
    public static readonly Pos None = new(Double.MinValue, Double.MinValue);
    public override string ToString() => $"({X:0.##},{Y:0.##})";
}

record Size(double Width, double Height)
{
    public static readonly Size Zero = new(0, 0);
    public static readonly Size None = new(Double.MinValue, Double.MinValue);
    public override string ToString() => $"({Width:0.##},{Height:0.##})";
}

record Rect(double X, double Y, double Width, double Height)
{
    public static readonly Rect Zero = new(0, 0, 0, 0);
    public static readonly Rect None = new(Double.MinValue, Double.MinValue, Double.MinValue, Double.MinValue);
    public override string ToString() => $"({X:0.##},{Y:0.##},{Width:0.##},{Height:0.##})";
}


record Color(int R, int G, int B)
{
    const int VeryDarkFactor = 8;
    const int Bright = 200;
    static readonly Random random = new();

    public static readonly Color Zero = new(0, 0, 0);
    public override string ToString() => $"#{R:x2}{G:x2}{B:x2}";

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


interface IItem { }
record Source(string Path, string Text, int LineNumber);

