namespace Dependinator.Shared;

record Coloring(int R, int G, int B)
{
    const int VeryDarkFactor = 12;
    const int EditFactor = 7;
    const int Bright = 200;
    static readonly Random random = new();

    public override string ToString() => $"#{R:x2}{G:x2}{B:x2}";
}
