namespace Dependinator.Models;

class Link : IItem
{
    readonly List<Line> lines = new();
    public Link(Node Source, Node Target)
    {
        this.Source = Source;
        this.Target = Target;
        this.Id = new LinkId(Source.Name, Target.Name);
    }

    public LinkId Id { get; }
    public Node Source { get; }
    public Node Target { get; }

    public void AddLine(Line line)
    {
        if (lines.Contains(line)) return;
        lines.Add(line);
    }

    public override string ToString() => $"{Source}->{Target} ({lines.Count})";
}




