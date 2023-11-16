namespace Dependinator.Models;

class Line : IItem
{
    readonly ModelBase model;
    readonly Dictionary<string, Link> links = new();

    public Line(Node source, Node target, ModelBase model)
    {
        Source = source;
        Target = target;
        this.model = model;
    }

    public Node Source { get; }
    public Node Target { get; }

    public string StrokeColor { get; set; } = "";
    public double StrokeWidth { get; set; } = 1.0;

    internal void Add(Link link)
    {
        links[link.Id] = link;
    }


    // public string GetSvg(Pos parentCanvasPos, double parentZoom)
    // {
    //     var (x1, y1) = From;
    //     var (x2, y2) = To;
    //     var s = StrokeWidth;

    //     return
    //         $"""
    //         <line x1="{x1}" y1="{y1}" x2="{x2}" y2="{y2}" stroke-width="{s}" stroke="{StrokeColor}"/>
    //         """;
    // }

    public override string ToString() => $"{Source}->{Target} ({links.Count})";

}
