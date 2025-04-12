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
        // Color = Models.Color.BrightRandom().ToString();
    }

    public ICollection<Link> Links => links.Values;
    public LineId Id { get; }
    public Node Source { get; }
    public Node Target { get; }
    public Rect Boundary
    {
        get
        {
            var (x1, y1, x2, y2) = GetLineEndpoints();

            return new Rect(Math.Min(x1, x2), Math.Min(y1, y2), Math.Max(x1, x2), Math.Max(y1, y2));
        }
    }

    public string Color { get; set; } = "red";
    public double StrokeWidth { get; set; } = 2.0;

    public void Add(Link link)
    {
        links[link.Id] = link;
    }

    public LinePos GetLineEndpoints()
    {
        var (s, t) = (Source.Boundary, Target.Boundary);

        return new LinePos(s.X + s.Width, s.Y + s.Height / 2, t.X, t.Y + t.Height / 2);
    }

    public override string ToString() => $"{Source}->{Target} ({links.Count})";
}
