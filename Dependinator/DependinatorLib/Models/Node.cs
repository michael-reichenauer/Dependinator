namespace Dependinator.Models;



class Node : IItem
{
    public Node(string name, Node parent)
    {
        Id = NodeId.FromName(name);
        Name = name;
        Parent = parent;

        var color = Color.BrightRandom();
        StrokeColor = color.ToString();
        Background = color.VeryDark().ToString();
        (LongName, ShortName) = NodeName.GetDisplayNames(name);
    }


    public const double DefaultContainerZoom = 1.0 / 7;

    public NodeId Id { get; }
    public string Name { get; }
    public Node Parent { get; private set; }
    public NodeType Type { get; set; } = NodeType.None;

    public string Description { get; set; } = "";
    public string StrokeColor { get; set; } = "";
    public string Background { get; set; } = "green";
    public double StrokeWidth { get; set; } = 1.0;
    public string IconName { get; set; } = "";

    public Rect Boundary { get; set; } = Rect.None;

    public List<Node> Children { get; } = new();
    public List<Link> SourceLinks { get; } = new();
    public List<Link> TargetLinks { get; } = new();
    public List<Line> SourceLines { get; } = new();
    public List<Line> TargetLines { get; } = new();

    public Double ContainerZoom { get; set; } = DefaultContainerZoom;
    //public Pos ContainerOffset { get; set; } = Pos.Zero;

    public bool IsRoot => Type == NodeType.Root;
    public string LongName { get; }
    public string ShortName { get; }

    public bool Update(Parsing.Node node)
    {
        if (IsEqual(node)) return false;

        Type = node.Type;
        Description = node.Description;
        return true;
    }

    public void AddChild(Node child)
    {
        Children.Add(child);
        child.Parent = this;
    }

    public void RemoveChild(Node child)
    {
        Children.Remove(child);
        child.Parent = null!;
    }

    public bool AddSourceLink(Link link)
    {
        if (SourceLinks.Contains(link)) return false;
        SourceLinks.Add(link);
        return true;
    }

    public void AddTargetLink(Link link)
    {
        if (TargetLinks.Contains(link)) return;
        TargetLinks.Add(link);
    }


    public IEnumerable<Node> Ancestors()
    {
        var node = this;
        while (node.Parent != null)
        {
            yield return node.Parent;
            node = node.Parent;
        }
    }



    bool IsEqual(Parsing.Node n) =>
        Parent.Name == n.ParentName &&
        Type == n.Type &&
        Description == n.Description;


    public override string ToString() => IsRoot ? "<root>" : LongName;
}
