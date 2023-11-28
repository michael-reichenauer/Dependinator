using Dependinator.Icons;


namespace Dependinator.Models;



class Node : NodeBase
{
    const double DefaultContainerZoom = 1.0 / 7;
    const double DefaultWidth = 100;
    const double DefaultHeight = 100;
    public static readonly Size DefaultSize = new(DefaultWidth, DefaultHeight);
    const double MinContainerZoom = 1.0;
    const double MaxNodeZoom = 30 * 1 / DefaultContainerZoom;           // To large to be seen
    private const int SmallIconSize = 9;
    private const int FontSize = 8;

    public string StrokeColor { get; set; } = "";
    public string Background { get; set; } = "green";
    public string LongName { get; }
    public string ShortName { get; }
    public double StrokeWidth { get; set; } = 1.0;
    public string IconName { get; set; } = "";

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


    public override string GetSvg(Pos parentCanvasPos, double parentZoom)
    {
        var nodeCanvasPos = GetNodeCanvasPos(parentCanvasPos, parentZoom);

        if ((parentZoom <= MinContainerZoom || // Too small to show children
            Type == Parsing.NodeType.Member)  // Members do not have children
            && !IsRoot)                       // Root have icon but can have children
        {
            return GetIconSvg(nodeCanvasPos, parentZoom);   // No children can be seen
        }
        else
        {
            var containerSvg = GetContainerSvg(nodeCanvasPos, parentZoom);
            var childrenZoom = parentZoom * ContainerZoom;
            return AllItems()
                .Select(n => n.GetSvg(nodeCanvasPos, childrenZoom))
                .Prepend(containerSvg).Join("\n");
        }
    }

    Pos GetNodeCanvasPos(Pos containerCanvasPos, double zoom) => new(
        containerCanvasPos.X + Boundary.X * zoom,
        containerCanvasPos.Y + Boundary.Y * zoom);


    string GetIconSvg(Pos nodeCanvasPos, double zoom)
    {
        if (zoom > MaxNodeZoom) return "";  // To large to be seen

        var (x, y) = nodeCanvasPos;
        var (w, h) = (Boundary.Width * zoom, Boundary.Height * zoom);
        var s = StrokeWidth;

        var (tx, ty) = (x + w / 2, y + h);
        var fz = FontSize * zoom;
        var icon = GetIconSvg();

        return
            $"""
            <use href="#{icon}" x="{x}" y="{y}" width="{w}" height="{h}" />
            <rect x="{x}" y="{y}" width="{w}" height="{h}" stroke-width="{s}" rx="2" fill="black" fill-opacity="0" stroke="none"><title>({x:0.##},{y:0.##},{w:0.##}, {h:0.##})</title></rect>
            <text x="{tx}" y="{ty}" class="iconName" font-size="{fz}px">{ShortName}</text>
            """;
    }


    string GetContainerSvg(Pos nodeCanvasPos, double zoom)
    {
        if (IsRoot) return "";
        if (zoom > MaxNodeZoom) return "";  // To large to be seen

        var s = StrokeWidth;
        var (x, y) = nodeCanvasPos;
        var (w, h) = (Boundary.Width * zoom, Boundary.Height * zoom);
        var (ix, iy, iw, ih) = (x, y + h + 1 * zoom, SmallIconSize * zoom, SmallIconSize * zoom);

        var (tx, ty) = (x + (SmallIconSize + 1) * zoom, y + h + 2 * zoom);
        var fz = FontSize * zoom;
        var icon = GetIconSvg();

        return
            $"""
            <rect x="{x}" y="{y}" width="{w}" height="{h}" stroke-width="{s}" rx="5" fill="{Background}" fill-opacity="1" stroke="{StrokeColor}"/>
            <use href="#{icon}" x="{ix}" y="{iy}" width="{iw}" height="{ih}" />
            <text x="{tx}" y="{ty}" class="nodeName" font-size="{fz}px">{ShortName}</text>
            """;
    }


    string GetIconSvg()
    {
        var icon = Icon.GetIconSvg(Type);
        return icon;
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
