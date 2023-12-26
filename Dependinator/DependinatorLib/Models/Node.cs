namespace Dependinator.Models;

class Node : IItem
{
    readonly NodeSvg svg;

    public Node(string name, Node parent)
    {
        Id = new NodeId(name);
        Name = name;
        Parent = parent;

        var color = Color.BrightRandom();
        StrokeColor = color.ToString();
        Background = color.VeryDark().ToString();
        (LongName, ShortName) = NodeName.GetDisplayNames(name);

        svg = new NodeSvg(this);
    }


    public NodeId Id { get; }
    public string Name { get; }
    public string LongName { get; }
    public string ShortName { get; }
    public string Description { get; set; } = "";

    public string StrokeColor { get; set; } = "";
    public string Background { get; set; } = "green";
    public double StrokeWidth { get; set; } = 1.0;
    public string IconName { get; set; } = "";

    public Rect Boundary { get; set; } = Rect.None;
    public Rect TotalBoundary => GetTotalBoundary();

    public List<Node> Children { get; } = new();
    public List<Link> SourceLinks { get; } = new();
    public List<Link> TargetLinks { get; } = new();
    public List<Line> SourceLines { get; } = new();
    public List<Line> TargetLines { get; } = new();

    public const double DefaultContainerZoom = 1.0 / 7;
    public Double ContainerZoom { get; set; } = DefaultContainerZoom;
    //public Pos ContainerOffset { get; set; } = Pos.Zero;

    public Node Parent { get; private set; }
    public bool IsRoot => Type == NodeType.Root;
    public NodeType Type { get; set; } = NodeType.None;


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


    public R<Node> FindNode(Pos parentCanvasPos, Pos pointCanvasPos, double parentZoom)
    {
        var nodeCanvasPos = GetNodeCanvasPos(parentCanvasPos, parentZoom);
        var nodePoint = GetNodeCanvasPos(pointCanvasPos, parentZoom);

        if (IsRoot) return FindNodeInChildren(nodeCanvasPos, nodePoint, parentZoom);


        var nodeCanvasRect = GetNodeCanvasRect(parentCanvasPos, parentZoom);
        if (!nodeCanvasRect.IsPosInside(nodePoint)) return R.None;

        if (svg.IsShowingChildren(parentZoom))
        {
            if (Try(out var child, FindNodeInChildren(nodeCanvasPos, nodePoint, parentZoom)))
                return child;
        }

        return this;
    }


    R<Node> FindNodeInChildren(Pos nodeCanvasPos, Pos pointCanvasPos, double parentZoom)
    {
        var childrenZoom = parentZoom * ContainerZoom;

        var node = Children.AsEnumerable().Reverse()
           .FirstOrDefault(child => child.FindNode(nodeCanvasPos, pointCanvasPos, childrenZoom));

        return node != null ? node : R.None;
    }


    public string GetSvg(Pos parentCanvasPos, double parentZoom) => svg.GetSvg(parentCanvasPos, parentZoom);


    public IEnumerable<IItem> AllItems()
    {
        foreach (var child in Children)
        {
            yield return child;
        }

        foreach (var child in Children)
        {
            foreach (var line in child.SourceLines)
            {
                yield return line;
            }
        }
    }

    public Pos GetNodeCanvasPos(Pos containerCanvasPos, double zoom) => new(
        containerCanvasPos.X + Boundary.X * zoom,
        containerCanvasPos.Y + Boundary.Y * zoom);

    public Rect GetNodeCanvasRect(Pos containerCanvasPos, double zoom) => new(
        containerCanvasPos.X + Boundary.X * zoom,
        containerCanvasPos.Y + Boundary.Y * zoom,
        Boundary.Width * zoom,
        Boundary.Height * zoom);



    bool IsEqual(Parsing.Node n) =>
        Parent.Name == n.ParentName &&
        Type == n.Type &&
        Description == n.Description;


    Rect GetTotalBoundary()
    {
        (double x1, double y1, double x2, double y2) =
            (double.MaxValue, double.MaxValue, double.MinValue, double.MinValue);
        foreach (var child in Children)
        {
            var b = child.Boundary;
            x1 = Math.Min(x1, b.X);
            y1 = Math.Min(y1, b.Y);
            x2 = Math.Max(x2, b.X + b.Width);
            y2 = Math.Max(y2, b.Y + b.Height);
        }

        return new Rect(x1, y1, x2 - x1, y2 - y1);
    }


    static bool IsOverlap(Rect r1, Rect r2)
    {
        // Check if one rectangle is to the left or above the other
        if (r1.X + r1.Width < r2.X || r2.X + r2.Width < r1.X) return false;
        if (r1.Y + r1.Height < r2.Y || r2.Y + r2.Height < r1.Y) return false;

        return true;
    }

    static Rect GetIntersection(Rect rect1, Rect rect2)
    {
        double x1 = Math.Max(rect1.X, rect2.X);
        double y1 = Math.Max(rect1.Y, rect2.Y);
        double x2 = Math.Min(rect1.X + rect1.Width, rect2.X + rect2.Width);
        double y2 = Math.Min(rect1.Y + rect1.Height, rect2.Y + rect2.Height);

        // Check if there is a valid intersection
        if (x1 < x2 && y1 < y2)
        {
            return new Rect(x1, y1, x2 - x1, y2 - y1);
        }

        return Rect.None;
    }


    public override string ToString() => IsRoot ? "<root>" : LongName;
}
