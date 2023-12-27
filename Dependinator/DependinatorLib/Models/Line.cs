namespace Dependinator.Models;

record LinePos(double X1, double Y1, double X2, double Y2);

class Line : IItem
{
    readonly Dictionary<Id, Link> links = new();

    public Line(Node source, Node target)
    {
        Source = source;
        Target = target;
        Id = new LineId(source.Name, target.Name);
        StrokeColor = Color.BrightRandom().ToString();
    }

    public LineId Id { get; }
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



    public string GetSvg(Pos parentCanvasPos, double zoom)
    {
        if (Node.IsToLargeToBeSeen(zoom)) return "";  

        var (x1, y1, x2, y2) = GetPos();

        if (Source != Target.Parent)
        {
            (x1, y1) = (parentCanvasPos.X + x1 * zoom, parentCanvasPos.Y + y1 * zoom);
        }
        else
        {
            (x1, y1) = (parentCanvasPos.X + x1 * zoom, parentCanvasPos.Y + y1 * zoom);
        }

        (x2, y2) = (parentCanvasPos.X + x2 * zoom, parentCanvasPos.Y + y2 * zoom);

        var s = StrokeWidth;

        return
            $"""
            <g class="hoverable" >
              <line x1="{x1}" y1="{y1}" x2="{x2}" y2="{y2}" stroke-width="{s}" stroke="white" marker-end="url(#arrow)" />
            </g>
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
