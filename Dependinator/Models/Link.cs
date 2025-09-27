namespace Dependinator.Models;

[Serializable]
record LinkDto(string SourceName, string TargetName, string TargetType);

class Link : IItem
{
    public List<Line> Lines { get; } = new();

    public Link(Node Source, Node Target)
    {
        this.Source = Source;
        this.Target = Target;
        this.Id = new LinkId(Source.Name, Target.Name);
    }

    public LinkDto ToDto() => new(Source.Name, Target.Name, Target.Type.Text);

    public LinkId Id { get; }
    public Node Source { get; }
    public Node Target { get; }
    public DateTime UpdateStamp { get; set; }

    public void AddLine(Line line)
    {
        if (Lines.Contains(line))
            return;
        Lines.Add(line);
    }

    public override string ToString() => $"{Source}->{Target} ({Lines.Count})";
}
