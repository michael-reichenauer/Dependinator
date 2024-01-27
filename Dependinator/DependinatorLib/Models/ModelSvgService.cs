namespace Dependinator.Models;


interface IModelSvgService
{
    LevelSvg GetSvg(Rect viewRect, double zoom);
}


[Transient]
class ModelSvgService : IModelSvgService
{
    static readonly double BatchSize = 1 * Math.Pow(10.0, 7);
    //const int BatchSize = 0;
    const int SmallIconSize = 9;
    const int FontSize = 8;

    const double MinContainerZoom = 1.0;
    const double MaxNodeZoom = 3 * 1 / Node.DefaultContainerZoom;           // To large to be seen

    readonly IModel model;

    public ModelSvgService(IModel model)
    {
        this.model = model;
    }

    static bool IsToLargeToBeSeen(double zoom) => zoom > MaxNodeZoom;

    static bool IsShowIcon(Node node, double zoom) =>
        node.Type == NodeType.Member || zoom <= MinContainerZoom;

    static Dictionary<int, LevelSvg> levels = new();

    public LevelSvg GetSvg(Rect viewRect, double zoom)
    {
        //Log.Info($"GetSvg: {viewRect} zoom: {zoom}");
        if (!model.Root.Children.Any()) return new LevelSvg(0, "", 1.0, Pos.Zero);

        int e = (int)Math.Floor(Math.Log(1 / zoom) / Math.Log(2.0));
        if (levels.TryGetValue(e, out var level))
        {
            Log.Info($"Reuse level {e}");
            return level;
        }

        var z = Math.Pow(2.0, e);

        Log.Info($"Level: {e} {z} {zoom} ");

        var svg = GetModelSvg(viewRect, z);
        Log.Info($"Svg: {svg.Length} chars");

        levels[e] = new LevelSvg(0, svg, 1 / z, new Pos(viewRect.X, viewRect.Y));
        return levels[e];

        // if (!model.svgContentData.levels.Any())
        // {
        //     model.svgContentData = GetSvgLevels();
        // }

        // return model.svgContentData.Get(zoom);
    }

    Svgs GetSvgLevels()
    {
        // var maxDeep = model.Items.Values.OfType<Node>().Max(n => n.Ancestors().Count());
        // Log.Info($"Max Deep: {maxDeep}");

        using var t = Timing.Start();

        var svgs = new List<LevelSvg>();

        for (int i = 0; i < 100; i++)
        {
            var zoom = Math.Pow(2.0, i);
            var rect = new Rect(0, 0, 1000, 1000);
            var svg = GetModelSvg(rect, zoom);
            if (svg == "") break;
            svgs.Add(new LevelSvg(i, svg, 1 / zoom, new Pos(rect.X, rect.Y)));
            // Log.Info($"Level: #{i} zoom: {zoom}, {1 / zoom} svg: {svg.Length} chars");
        }
        Log.Info($"Levels: {svgs.Count}");

        return new Svgs(svgs);
    }

    string GetModelSvg(Rect rect, double zoom)
    {
        using var t = Timing.Start($"GetModelSvg: {rect}, {zoom}");
        var offset = new Pos(rect.X, rect.Y);
        return GetNodeContentSvg(model.Root, offset, zoom);
    }


    static string GetNodeSvg(Node node, Pos parentCanvasPos, double zoom)
    {
        var nodeCanvasPos = GetNodeCanvasPos(node, parentCanvasPos, zoom);

        // var nodeCanvasRect = GetNodeCanvasRect(node, parentCanvasPos, zoom);

        // var batchCanvasRect = new Rect(0, 0, BatchSize, BatchSize);

        if (IsToLargeToBeSeen(zoom)) return GetNodeContentSvg(node, nodeCanvasPos, zoom);

        // if (!IsOverlap(batchCanvasRect, nodeCanvasRect))
        // {
        //     IsOverlap(batchCanvasRect, nodeCanvasRect);
        //     Log.Info($"{node.LongName}, #: {node.Ancestors().Count()}, {nodeCanvasRect}");
        // }

        if (IsShowIcon(node, zoom)) return GetNodeIconSvg(node, nodeCanvasPos, zoom);

        return
            GetNodeContainerSvg(node, nodeCanvasPos, zoom) +
            GetNodeContentSvg(node, nodeCanvasPos, zoom);
    }


    static string GetNodeContentSvg(Node node, Pos nodeCanvasPos, double zoom)
    {
        var childrenZoom = zoom * node.ContainerZoom;

        return
            node.Children.Select(n => GetNodeSvg(n, nodeCanvasPos, childrenZoom))
            // .Concat(GetNodeLinesSvg(node, nodeCanvasPos, childrenZoom))
            .Join("");
    }


    static IEnumerable<string> GetNodeLinesSvg(Node node, Pos nodeCanvasPos, double childrenZoom)
    {
        // !!! Must also add parent to children lines

        foreach (var child in node.Children)
        {
            foreach (var line in child.SourceLines)
            {
                yield return GetLineSvg(line, nodeCanvasPos, childrenZoom);
            }
        }
    }

    static Pos GetNodeCanvasPos(Node node, Pos parentCanvasPos, double zoom) => new(
       parentCanvasPos.X + node.Boundary.X * zoom,
       parentCanvasPos.Y + node.Boundary.Y * zoom);


    static Rect GetNodeCanvasRect(Node node, Pos containerCanvasPos, double zoom) => new(
       containerCanvasPos.X + node.Boundary.X * zoom,
       containerCanvasPos.Y + node.Boundary.Y * zoom,
       node.Boundary.Width * zoom,
       node.Boundary.Height * zoom);



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

    static Rect GetTotalBoundary(Node node)
    {
        (double x1, double y1, double x2, double y2) =
            (double.MaxValue, double.MaxValue, double.MinValue, double.MinValue);
        foreach (var child in node.Children)
        {
            var b = child.Boundary;
            x1 = Math.Min(x1, b.X);
            y1 = Math.Min(y1, b.Y);
            x2 = Math.Max(x2, b.X + b.Width);
            y2 = Math.Max(y2, b.Y + b.Height);
        }

        return new Rect(x1, y1, x2 - x1, y2 - y1);
    }


    static string GetNodeIconSvg(Node node, Pos nodeCanvasPos, double parentZoom)
    {
        var (x, y) = nodeCanvasPos;
        var (w, h) = (node.Boundary.Width * parentZoom, node.Boundary.Height * parentZoom);

        var (tx, ty) = (x + w / 2, y + h);
        var fz = FontSize * parentZoom;
        var icon = node.Type.IconName;

        return
            $"""
            <use href="#{icon}" x="{x}" y="{y}" width="{w}" height="{h}" />
            <text x="{tx}" y="{ty}" class="iconName" font-size="{fz}px">{node.ShortName}</text>
            <g class="hoverable">
              <rect id="{node.Id.Value}" x="{x - 2}" y="{y - 2}" width="{w + 2}" height="{h + 2}" stroke-width="1" rx="2" fill="black" fill-opacity="0" stroke="none"/>
            </g>
            </rect>
            """;
    }


    static string GetNodeContainerSvg(Node node, Pos nodeCanvasPos, double parentZoom)
    {
        var s = node.StrokeWidth;
        var (x, y) = nodeCanvasPos;
        var (w, h) = (node.Boundary.Width * parentZoom, node.Boundary.Height * parentZoom);
        var (ix, iy, iw, ih) = (x, y + h + 1 * parentZoom, SmallIconSize * parentZoom, SmallIconSize * parentZoom);

        var (tx, ty) = (x + (SmallIconSize + 1) * parentZoom, y + h + 2 * parentZoom);
        var fz = FontSize * parentZoom;
        var icon = node.Type.IconName;

        //Log.Info($"{node.ShortName} ({x:E4},{y:E4},{w},{h}) ,{node.Boundary} ");

        return
            $"""
            <rect x="{x}" y="{y}" width="{w}" height="{h}" stroke-width="{s}" rx="5" fill="{node.Background}" fill-opacity="1" stroke="{node.StrokeColor}"/>      
            <use href="#{icon}" x="{ix}" y="{iy}" width="{iw}" height="{ih}" />
            <text x="{tx}" y="{ty}" class="nodeName" font-size="{fz}px">{node.ShortName}</text>
            <g class="hoverable">
              <rect id="{node.Id.Value}" x="{x - 2}" y="{y - 2}" width="{w + 2}" height="{h + 2}" stroke-width="1" rx="2" fill="black" fill-opacity="0" stroke="none"/>
            </g>
            """;
    }

    static string GetLineSvg(Line line, Pos parentCanvasPos, double zoom)
    {
        if (IsToLargeToBeSeen(zoom)) return "";

        var (x1, y1, x2, y2) = line.GetLineEndpoints();

        // !!!!! Fel i koden, samma rad i if och else delen 
        if (line.Source != line.Target.Parent)
        {
            (x1, y1) = (parentCanvasPos.X + x1 * zoom, parentCanvasPos.Y + y1 * zoom);
        }
        else
        {
            (x1, y1) = (parentCanvasPos.X + x1 * zoom, parentCanvasPos.Y + y1 * zoom);
        }

        (x2, y2) = (parentCanvasPos.X + x2 * zoom, parentCanvasPos.Y + y2 * zoom);

        var s = line.StrokeWidth;

        return
            $"""
            <g class="hoverable" >
              <line x1="{x1}" y1="{y1}" x2="{x2}" y2="{y2}" stroke-width="{s}" stroke="white" marker-end="url(#arrow)" />
            </g>
            """;
    }
}
