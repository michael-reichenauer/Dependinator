namespace Dependinator.Models;

class Line : IItem
{
    const double DefaultContainerZoom = 1.0 / 7;
    const double MaxNodeZoom = 30 * 1 / DefaultContainerZoom;           // To large to be seen

    readonly ModelBase model;
    readonly Dictionary<string, Link> links = new();

    public Line(Node source, Node target, ModelBase model)
    {
        Source = source;
        Target = target;
        this.model = model;
        StrokeColor = Color.BrightRandom().ToString();
    }

    public Node Source { get; }
    public Node Target { get; }
    public Rect Boundary
    {
        get
        {
            var s = SourcePos;
            var t = TargetPos;
            return new Rect(Math.Min(s.X, t.X), Math.Min(s.Y, t.Y), Math.Max(s.X, t.X), Math.Max(s.Y, t.Y));
        }
    }


    public string StrokeColor { get; set; } = "red";
    public double StrokeWidth { get; set; } = 1.0;

    internal void Add(Link link)
    {
        links[link.Id] = link;
    }

    Pos SourcePos => new(Source.Boundary.X, Source.Boundary.Y);
    Pos TargetPos => new(Target.Boundary.X, Target.Boundary.Y);


    public string GetSvg(Pos parentCanvasPos, double parentZoom)
    {
        if (parentZoom > MaxNodeZoom) return "";    // Too large to show


        var (x1, y1) = SourcePos;
        var (x2, y2) = TargetPos;

        (x1, y1) = (parentCanvasPos.X + x1 * parentZoom, parentCanvasPos.Y + y1 * parentZoom);
        (x2, y2) = (parentCanvasPos.X + x2 * parentZoom, parentCanvasPos.Y + y2 * parentZoom);

        var s = StrokeWidth;

        return
            $"""
            <line x1="{x1}" y1="{y1}" x2="{x2}" y2="{y2}" stroke-width="{s}" stroke="{StrokeColor}"/>
            """;
    }


    public override string ToString() => $"{Source}->{Target} ({links.Count})";
}
