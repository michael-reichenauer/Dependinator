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


interface IItem { }
record Source(string Path, string Text, int LineNumber);

