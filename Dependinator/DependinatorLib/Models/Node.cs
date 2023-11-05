namespace Dependinator.Models;

class Node : NodeBase
{
    const double DefaultContainerZoom = 1.0 / 7;
    const double DefaultWidth = 100;
    const double DefaultHeight = 100;
    public static readonly Size DefaultSize = new(DefaultWidth, DefaultHeight);
    const double MinZoom = 0.1;
    const double MaxZoom = 5.0;

    public string StrokeColor { get; set; } = "";
    public string Background { get; set; } = "green";
    public string LongName { get; }
    public string ShortName { get; }
    public double StrokeWidth { get; set; } = 1.0;

    public Rect Boundary { get; set; } = Rect.None;
    public Rect TotalBoundary => GetTotalBoundary();

    public Double ContainerZoom { get; set; } = DefaultContainerZoom;
    //public Pos ContainerOffset { get; set; } = Pos.Zero;

    public Node(string name, Node parent, ModelBase model)
    : base(name, parent, model)
    {
        var color = Color.BrightRandom();
        StrokeColor = color.ToString();
        Background = color.VeryDark().ToString();
        (LongName, ShortName) = NodeName.GetDisplayNames(name);
    }


    public string GetSvg(Pos containerCanvasPos, double zoom)
    {
        if (zoom < MinZoom) return "";  // Too small to be seen

        var nodeCanvasPos = GetNodeCanvasPos(containerCanvasPos, zoom);
        if (zoom < 1) return GetIconSvg(nodeCanvasPos, zoom);   // No children can be seen

        var containerSvg = GetContainerSvg(nodeCanvasPos, zoom);
        var nodeSvg = GetChildrenSvgs(nodeCanvasPos, zoom).Prepend(containerSvg).Join("\n");
        return nodeSvg;
    }

    Pos GetNodeCanvasPos(Pos containerCanvasPos, double zoom) => new(
        containerCanvasPos.X + Boundary.X * zoom,
        containerCanvasPos.Y + Boundary.Y * zoom);


    string GetIconSvg(Pos nodeCanvasPos, double zoom)
    {
        if (IsRoot) return "";

        var (x, y) = nodeCanvasPos;
        var (w, h) = (Boundary.Width * zoom, Boundary.Height * zoom);

        var (tx, ty) = (x + 2, y + h + 1);
        var fz = 8 * zoom;

        return
            $"""
            <use href="#DefaultIcon" x="{x}" y="{y}" width="{w}" height="{h}" />
            <text x="{tx}" y="{ty}" class="nodeName" font-size="{fz}px">{ShortName}</text>
            """;
    }


    string GetContainerSvg(Pos nodeCanvasPos, double zoom)
    {
        if (IsRoot) return "";
        if (zoom > MaxZoom) return "";  // To large to be seen

        var s = StrokeWidth * zoom;
        var (x, y) = nodeCanvasPos;
        var (w, h) = (Boundary.Width * zoom, Boundary.Height * zoom);

        var (tx, ty) = (x, y + h / 2);
        var fz = 8 * zoom;
        var background = Background;

        return
            $"""
            <rect x="{x}" y="{y}" width="{w}" height="{h}" stroke-width="{s}" rx="0" fill="{background}" fill-opacity="1" stroke="{StrokeColor}"/>
            <text x="{tx}" y="{ty}" class="nodeName" font-size="{fz}px">{ShortName}</text>
            """;
    }


    IEnumerable<string> GetChildrenSvgs(Pos nodeCanvasPos, double zoom)
    {
        var childrenZoom = zoom * ContainerZoom;

        return Children.Select(n => n.GetSvg(nodeCanvasPos, childrenZoom));
    }


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

