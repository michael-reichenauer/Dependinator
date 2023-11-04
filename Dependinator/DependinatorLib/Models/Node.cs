namespace Dependinator.Models;

class Node : NodeBase
{
    const double DefaultWidth = 100;
    const double DefaultHeight = 100;
    public static readonly Size DefaultSize = new(DefaultWidth, DefaultHeight);
    const double MinZoom = 0.001;

    public string StrokeColor { get; set; } = "";
    public string Background { get; set; } = "green";
    public string LongName { get; }
    public string ShortName { get; }
    public double FillOpacity { get; set; } = 1;
    public double StrokeWidth { get; set; } = 1.0;

    public Rect Boundary { get; set; } = Rect.None;
    public Rect TotalBoundary => GetTotalBoundary();

    public Double ContainerZoom { get; set; } = 1.0 / 7;
    //public Pos ContainerOffset { get; set; } = Pos.Zero;

    public Node(string name, Node parent, ModelBase model)
    : base(name, parent, model)
    {
        var color = Color.BrightRandom();
        StrokeColor = color.ToString();
        Background = color.VeryDark().ToString();
        (LongName, ShortName) = NodeName.GetDisplayNames(name);
    }


    public string GetSvg(Rect parentCanvasBounds, double zoom)
    {
        var nodeCanvasBounds = GetCanvasBounds(parentCanvasBounds, zoom);
        //if (nodeCanvasBounds.Width < 5 || nodeCanvasBounds.Height < 5) return "";   // Too small to be seen

        // Adjust bound to be intersection of parent and node bounds     #####
        // Skip if node is outside parent canvas bounds and not visible  #####

        var nodeSvg = GetNodeSvg(nodeCanvasBounds, zoom);

        // if (nodeCanvasBounds.Width < 50 || nodeCanvasBounds.Height < 50) return nodeSvg;  // To small for children to be seen

        var svg = GetChildrenSvgs(nodeCanvasBounds, zoom).Prepend(nodeSvg).Join("\n");

        return svg;
    }


    string GetNodeSvg(Rect canvasBounds, double zoom)
    {
        if (IsRoot) return "";

        var s = StrokeWidth * zoom;
        var (x, y, w, h) = canvasBounds;
        var (tx, ty) = (x, y + h / 2);
        var fz = 8 * zoom;

        return
            $"""
            <rect x="{x}" y="{y}" width="{w}" height="{h}" stroke-width="{s}" rx="0" fill="{Background}" fill-opacity="{FillOpacity}" stroke="{StrokeColor}"/>
            <text x="{tx}" y="{ty}" class="nodeName" font-size="{fz}px">{ShortName}</text>
            """;
    }

    IEnumerable<string> GetChildrenSvgs(Rect nodeCanvasBounds, double zoom)
    {
        var childrenZoom = zoom * ContainerZoom;

        var childrenCanvasBounds = new Rect(
            nodeCanvasBounds.X,
            nodeCanvasBounds.Y,
            nodeCanvasBounds.Width / ContainerZoom,
            nodeCanvasBounds.Height / ContainerZoom
        );

        return Children.Select(n => n.GetSvg(childrenCanvasBounds, childrenZoom));
    }

    Rect GetCanvasBounds(Rect parentCanvasBounds, double zoom)
    {
        var w = Boundary.Width * zoom;
        var h = Boundary.Height * zoom;
        var x = parentCanvasBounds.X + Boundary.X * zoom;
        var y = parentCanvasBounds.Y + Boundary.Y * zoom;
        return new Rect(x, y, w, h);
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

