using Dependinator.UI.Modeling.Dtos;
using Dependinator.UI.Shared.Types;

namespace Dependinator.UI.Modeling.Models;

class Link : IItem
{
    public List<Line> Lines { get; } = new();

    public Link(Node Source, Node Target)
    {
        this.Source = Source;
        this.Target = Target;
        this.Id = new LinkId(Source.Name, Target.Name);
    }

    public LinkDto ToDto() =>
        new(Source.Name, Target.Name, Target.Type.ToString()) { IsManual = IsManual, IsInheritance = IsInheritance };

    public LinkId Id { get; }
    public Node Source { get; }
    public Node Target { get; }
    public DateTime UpdateStamp { get; set; }

    // A manually added link (drawn by the user), exempt from stale-link removal on re-parse.
    public bool IsManual { get; set; }

    // The source type inherits/implements the target type (UML generalization/realization).
    public bool IsInheritance { get; set; }

    public void AddLine(Line line)
    {
        if (Lines.Contains(line))
            return;
        Lines.Add(line);
    }

    public override string ToString() => $"{Source}->{Target} ({Lines.Count})";
}
