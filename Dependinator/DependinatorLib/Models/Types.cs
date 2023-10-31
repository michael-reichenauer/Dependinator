using System.Security.Cryptography.X509Certificates;

namespace Dependinator.Models;

record Pos(double X, double Y)
{
    public static readonly Pos Zero = new(0, 0);
    public static readonly Pos None = new(Double.MinValue, Double.MinValue);
    public override string ToString() => $"({X},{Y})";
}

record Size(double Width, double Height)
{
    public static readonly Size Zero = new(0, 0);
    public static readonly Size None = new(Double.MinValue, Double.MinValue);
    public override string ToString() => $"({Width},{Height})";
}

record Rect(double X, double Y, double Width, double Height)
{
    public static readonly Rect Zero = new(0, 0, 0, 0);
    public static readonly Rect None = new(Double.MinValue, Double.MinValue, Double.MinValue, Double.MinValue);
    public override string ToString() => $"({X},{Y},{Width},{Height})";
}

record Color(int R, int G, int B)
{
    const int VeryDarkFactor = 7;
    static readonly Random random = new();

    public static readonly Color Zero = new(0, 0, 0);
    public override string ToString() => $"#{R:x2}{G:x2}{B:x2}";
    public static Color Random() => new(random.Next(0, 256), random.Next(0, 256), random.Next(0, 256));
    public Color VeryDark() => new(R / VeryDarkFactor, G / VeryDarkFactor, B / VeryDarkFactor);
}


interface IItem { }
record Source(string Path, string Text, int LineNumber);

