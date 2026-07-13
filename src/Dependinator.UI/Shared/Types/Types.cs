// Common lightweight value types shared across the UI, such as Pos, Rect, Source, FileLocation,
// and strongly-typed ids.
namespace Dependinator.UI.Shared.Types;

interface IItem { }

public record Source(string Text, FileLocation Location);

public record FileLocation(string Path, int Line);

record Pos(double X, double Y)
{
    // Note: None equals a genuine origin position and cannot be distinguished from it.
    public static readonly Pos None = new(0, 0);

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
}
