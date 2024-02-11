using System.Web;

namespace Dependinator.Models;



class Node : IItem
{
    public Node(string name, Node parent)
    {
        Id = NodeId.FromName(name);
        Name = name;
        Parent = parent;

        var color = Models.Color.BrightRandom();
        Color = color.ToString();
        Background = color.VeryDark().ToString();
        (LongName, ShortName) = NodeName.GetDisplayNames(name);
        HtmlShortName = HttpUtility.HtmlEncode(ShortName);
        HtmlLongName = HttpUtility.HtmlEncode(LongName);
    }


    public const double DefaultContainerZoom = 1.0 / 7;

    public NodeId Id { get; }
    public string Name { get; }
    public Node Parent { get; private set; }
    public NodeType Type { get; set; } = NodeType.None;

    public string Description { get; set; } = "";
    public string Color { get; set; } = "";
    public string Background { get; set; } = "green";
    public double StrokeWidth { get; set; } = 1.0;
    public bool IsSelected { get; set; } = false;

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
    public string HtmlShortName { get; }
    public string HtmlLongName { get; }

    public double GetZoom()
    {
        var zoom = 1.0;
        Ancestors().ForEach(n => zoom *= 1 / n.ContainerZoom);
        return zoom;
    }

    public void Update(Parsing.Node node)
    {
        Type = node.Type;
        Description = node.Description ?? Description;
        var rect = node.X != null
            ? new Rect(node.X.Value, node.Y!.Value, node.Width!.Value!, node.Height!.Value)
            : Boundary;
        Boundary = rect;
        ContainerZoom = node.Zoom ?? ContainerZoom;
        Color = node.Color ?? Color;
        Background = node.Background ?? Background;
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



    public override string ToString() => IsRoot ? "<root>" : LongName;
}
