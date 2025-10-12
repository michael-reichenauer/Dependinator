namespace Dependinator.Models;

record LinePos(double X1, double Y1, double X2, double Y2);

class Line : IItem
{
    readonly Dictionary<Id, Link> links = new();

    public Line(Node source, Node target, bool isDirect = false, LineId? id = null)
    {
        Source = source;
        Target = target;
        Id = id ?? (isDirect ? LineId.FromDirect(source.Name, target.Name) : LineId.From(source.Name, target.Name));
        IsDirect = isDirect;
    }

    public ICollection<Link> Links => links.Values;
    public LineId Id { get; }
    public Node Source { get; }
    public Node Target { get; }
    public bool IsEmpty => links.Count == 0;
    public bool IsDirect { get; }
    public Node? RenderAncestor { get; set; }

    public string Color { get; set; } = "red";
    public double StrokeWidth { get; set; } = 1.5;
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
