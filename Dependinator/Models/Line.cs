namespace Dependinator.Models;

record LinePos(double X1, double Y1, double X2, double Y2);

class Line : IItem
{
    readonly Dictionary<Id, Link> links = new();

    public Line(Node source, Node target)
    {
        Source = source;
        Target = target;
        Id = LineId.From(source.Name, target.Name);
        // Color = Models.Color.BrightRandom().ToString();
    }

    public ICollection<Link> Links => links.Values;
    public LineId Id { get; }
    public Node Source { get; }
    public Node Target { get; }
    public bool IsEmpty => links.Count == 0;

    public string Color { get; set; } = "red";
    public double StrokeWidth { get; set; } = 2.0;
    public bool IsSelected { get; internal set; }
    public string HtmlShortName => $"{Source.HtmlShortName}→{Target.HtmlShortName}";

    public bool IsUpHill { get; internal set; } // Used to determine the direction of the line for placing the toolbar
    public bool IsHidden { get; internal set; }

    public void Add(Link link)
    {
        links[link.Id] = link;
    }

    public void Remove(Link link)
    {
        links.Remove(link.Id);
    }

    public override string ToString() => $"{Source}->{Target} ({links.Count})";
}
