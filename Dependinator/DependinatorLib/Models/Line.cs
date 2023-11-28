namespace Dependinator.Models;

record LinePos(double X1, double Y1, double X2, double Y2);

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
            var (x1, y1, x2, y2) = GetPos();

            return new Rect(Math.Min(x1, x2), Math.Min(y1, y2), Math.Max(x1, x2), Math.Max(y1, y2));
        }
    }


    public string StrokeColor { get; set; } = "red";
    public double StrokeWidth { get; set; } = 1.0;

    internal void Add(Link link)
    {
        links[link.Id] = link;
    }



    public string GetSvg(Pos parentCanvasPos, double parentZoom)
    {
        if (parentZoom > MaxNodeZoom) return "";    // Too large to show

        var (x1, y1, x2, y2) = GetPos();

        (x1, y1) = (parentCanvasPos.X + x1 * parentZoom, parentCanvasPos.Y + y1 * parentZoom);
        (x2, y2) = (parentCanvasPos.X + x2 * parentZoom, parentCanvasPos.Y + y2 * parentZoom);

        var s = StrokeWidth;

        return
            $"""
            <line x1="{x1}" y1="{y1}" x2="{x2}" y2="{y2}" stroke-width="{s}" stroke="white" marker-end="url(#arrow)" />
            """;
    }

    LinePos GetPos()
    {
        var (s, t) = (Source.Boundary, Target.Boundary);

        if (s.Y + s.Height <= t.Y)
        {
            return new LinePos(s.X + s.Width / 2, s.Y + s.Height, t.X + t.Width / 2, t.Y);
        }

        return new LinePos(s.X + s.Width, s.Y + s.Height / 2, t.X, t.Y + t.Height / 2);
    }


    public override string ToString() => $"{Source}->{Target} ({links.Count})";
}
