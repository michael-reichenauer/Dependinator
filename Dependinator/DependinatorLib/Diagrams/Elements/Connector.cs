namespace Dependinator.Diagrams.Elements;

class Connector : IElement
{
    string Id { get; set; } = "";
    string Name { get; set; } = "";
    public int X1 { get; set; }
    public int Y1 { get; set; }
    public int X2 { get; set; }
    public int Y2 { get; set; }
    public List<Pos> More { get; set; } = new List<Pos>();
    public string Color { get; set; } = "";

    public string Svg { get; private set; } = "";
    public int X { get; private set; }
    public int Y { get; private set; }
    public int W { get; private set; }
    public int H { get; private set; }



    public void Update()
    {
        X = More.Select(p => X).Add(X1, X2).Min();
        Y = More.Select(p => Y).Add(Y1, Y2).Min();
        W = More.Select(p => X).Add(X1, X2).Max() - X;
        H = More.Select(p => Y).Add(Y1, Y2).Max() - Y;
        var m = More.Any() ? More.Select(p => $"{p.X},{p.Y}").Join(" ") : "";

        Svg = $"""<polyline points="{X1},{Y1} {m} {X2},{Y2}" fill="none" stroke="{Color}" />""";
    }

    public override string ToString() => $"({X},{Y}, {W},{H})";
}
