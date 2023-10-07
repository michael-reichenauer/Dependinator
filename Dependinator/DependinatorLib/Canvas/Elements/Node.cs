namespace DependinatorLib.Diagrams.Elements;

// https://developer.mozilla.org/en-US/docs/Web/SVG/Tutorial/Fills_and_Strokes
// Use <defs> and style to create hoover effects and global styles to avoid repeating
// Gradients 
// https://developer.mozilla.org/en-US/docs/Web/SVG/Tutorial/Texts 
// for texts
// Embedding SVG in HTML
// https://developer.mozilla.org/en-US/docs/Web/SVG/Tutorial/Basic_Transformations
class Node : IElement
{
    string Id { get; set; } = "";
    string Name { get; set; } = "";
    public int X { get; set; }
    public int Y { get; set; }
    public int W { get; set; }
    public int H { get; set; }
    public int RX { get; set; } = 5;
    public string Color { get; set; } = "";
    public string Background { get; set; } = "green";

    public string Svg { get; private set; } = "";

    public void Update()
    {
        Svg = $"""<rect x="{X}" y="{Y}" width="{W}" height="{H}" rx="{RX}" fill="{Background}" fill-opacity="0.2" stroke="{Color}" stroke-width="2"/>""";
    }

    public override string ToString() => $"({X},{Y}, {W},{H})";
}
