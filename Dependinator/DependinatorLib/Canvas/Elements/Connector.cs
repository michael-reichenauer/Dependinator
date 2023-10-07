namespace DependinatorLib.Diagrams.Elements;

class Connector : IElement
{
    string Id { get; set; } = "";
    string Name { get; set; } = "";
    public int X1 { get; set; }
    public int Y1 { get; set; }
    public int X2 { get; set; }
    public int Y2 { get; set; }
    public string Color { get; set; } = "";

    public string Svg { get; private set; } = "";

    public void Update()
    {
        Svg = $"""<polyline points="{X1} {Y1} {X1} {Y1 + 20} {X2} {Y2}" stroke="{Color}" stroke-width="1.5" fill="transparent" style="pointer-events:none !important;" stroke-width="3"/ stroke-linejoin="round">""";
    }
}
