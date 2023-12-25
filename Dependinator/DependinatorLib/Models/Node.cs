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

    public string StrokeColor { get; set; } = "";
    public string Background { get; set; } = "green";
    public string LongName { get; }
    public string ShortName { get; }
    public double StrokeWidth { get; set; } = 1.0;
    public string IconName { get; set; } = "";

    public Rect Boundary { get; set; } = Rect.None;
    public Rect TotalBoundary => GetTotalBoundary();

    public List<Node> children { get; } = new();
    public List<Link> sourceLinks { get; } = new();
    public List<Link> targetLinks { get; } = new();
    public List<Line> sourceLines { get; } = new();
    public List<Line> targetLines { get; } = new();

    public const double DefaultContainerZoom = 1.0 / 7;
    public Double ContainerZoom { get; set; } = DefaultContainerZoom;
    //public Pos ContainerOffset { get; set; } = Pos.Zero;


    public Node Parent { get; private set; }
    public bool IsRoot => Type == Parsing.NodeType.Root;
    public Parsing.NodeType Type { get; set; } = Parsing.NodeType.None;
    public string Description { get; set; } = "";



    public void AddChild(Node child)
    {
        children.Add(child);
        child.Parent = (Node)this;
    }


    public void RemoveChild(Node child)
    {
        children.Remove(child);
        child.Parent = null!;
    }

    public bool AddSourceLink(Link link)
    {
        if (sourceLinks.Contains(link)) return false;
        sourceLinks.Add(link);
        return true;
    }

    public void AddTargetLink(Link link)
    {
        if (targetLinks.Contains(link)) return;
        targetLinks.Add(link);
    }


    public bool Update(Parsing.Node node)
    {
        if (IsEqual(node)) return false;
        Type = node.Type;
        Description = node.Description;
        return true;

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




    public R<Node> FindNode(Pos parentCanvasPos, Pos pointCanvasPos, double parentZoom)
    {
        var nodeCanvasPos = GetNodeCanvasPos(parentCanvasPos, parentZoom);
        var nodeCanvasRect = GetNodeCanvasRect(parentCanvasPos, parentZoom);

        if (!IsRoot && !nodeCanvasRect.IsPosInside(pointCanvasPos)) return R.None;

        if ((parentZoom <= NodeSvg.MinContainerZoom || // Too small to show children
            Type == Parsing.NodeType.Member)  // Members do not have children
            && !IsRoot)                       // Root have icon but can have children
        {
            return this;
        }

        var childrenZoom = parentZoom * ContainerZoom;
        foreach (var child in children.AsEnumerable().Reverse())
        {
            if (!Try(out var node, child.FindNode(nodeCanvasPos, pointCanvasPos, childrenZoom))) continue;
            return node;
        }

        if (IsRoot) return R.None;
        return this;
    }


    public string GetSvg(Pos parentCanvasPos, double parentZoom) => svg.GetSvg(parentCanvasPos, parentZoom);


    public IEnumerable<IItem> AllItems()
    {
        foreach (var child in children)
        {
            yield return child;
        }
        foreach (var child in children)
        {
            foreach (var line in child.sourceLines)
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





    Rect GetTotalBoundary()
    {
        (double x1, double y1, double x2, double y2) =
            (double.MaxValue, double.MaxValue, double.MinValue, double.MinValue);
        foreach (var child in children)
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
