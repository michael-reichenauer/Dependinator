namespace Dependinator.Models;

interface IItem { }

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
