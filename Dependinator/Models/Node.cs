using System.Web;

namespace Dependinator.Models;



class Node : IItem
{
    const double MinContainerZoom = 2.0;
    const double MaxNodeZoom = 5 * 1 / Node.DefaultContainerZoom;           // To large to be seen

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


    public const double DefaultContainerZoom = 1.0 / 8;

    public NodeId Id { get; }
    public string Name { get; }
    public Node Parent { get; private set; }
    public NodeType Type { get; set; } = NodeType.None;

    public string Description { get; set; } = "";
    public string Color { get; set; } = "";
    public string Background { get; set; } = "green";
    public double StrokeWidth { get; set; } = 2;
    public bool IsSelected { get; set; } = false;
    public bool IsChildrenLayoutRequired { get; set; } = false;

    public Rect Boundary { get; set; } = Rect.None;

    public List<Node> Children { get; } = new();
    public List<Link> SourceLinks { get; } = new();
    public List<Link> TargetLinks { get; } = new();
    public List<Line> SourceLines { get; } = new();
    public List<Line> TargetLines { get; } = new();

    public Double ContainerZoom { get; set; } = DefaultContainerZoom;
    public Pos ContainerOffset { get; set; } = Pos.None;

    public bool IsRoot => Type == NodeType.Root;
    public string LongName { get; }
    public string ShortName { get; }
    public string HtmlShortName { get; }
    public string HtmlLongName { get; }

    public static bool IsToLargeToBeSeen(double zoom) => zoom > MaxNodeZoom;

    public bool IsShowIcon(double zoom) =>
        Type == NodeType.Member || zoom <= MinContainerZoom;

    public double GetZoom()
    {
        var zoom = 1.0;
        Ancestors().ForEach(n => zoom *= 1 / n.ContainerZoom);
        return zoom;
    }

    internal Rect GetTotalBounds()
    {
        // Calculate the total bounds of the children
        (double x1, double y1, double x2, double y2) = Children.Aggregate(
            (double.MaxValue, double.MaxValue, double.MinValue, double.MinValue),
            (bounds, child) =>
            {
                var (x1, y1, x2, y2) = bounds;
                var cb = child.Boundary;
                var (cx1, cy1, cx2, cy2) = (cb.X, cb.Y, cb.X + cb.Width, cb.Y + cb.Height);
                return (Math.Min(x1, cx1), Math.Min(y1, cy1), Math.Max(x2, cx2), Math.Max(y2, cy2));
            });

        return new Rect(x1, y1, x2 - x1, y2 - y1);
    }

    public void Update(Parsing.Node node)
    {
        Type = node.Type;
        Description = node.Description ?? Description;
        var rect = node.X != null
            ? new Rect(node.X.Value, node.Y!.Value, node.Width!.Value!, node.Height!.Value)
            : Boundary;
        var offset = node.OffsetX != null
            ? new Pos(node.OffsetX.Value, node.OffsetY!.Value)
            : ContainerOffset;
        Boundary = rect;
        ContainerOffset = offset;
        ContainerZoom = node.Zoom ?? ContainerZoom;
        Color = node.Color ?? Color;
        Background = node.Background ?? Background;
    }

    public void AddChild(Node child)
    {
        if (child.Boundary == Rect.None)
        {
            IsChildrenLayoutRequired = true;
        }

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
