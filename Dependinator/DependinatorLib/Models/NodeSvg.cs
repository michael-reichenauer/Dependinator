using Dependinator.Icons;


namespace Dependinator.Models;

class NodeSvg
{
    public const double MinContainerZoom = 1.0;
    const double MaxNodeZoom = 30 * 1 / Node.DefaultContainerZoom;           // To large to be seen
    const int SmallIconSize = 9;
    const int FontSize = 8;

    readonly Node node;

    public NodeSvg(Node node)
    {
        this.node = node;
    }

    public string GetSvg(Pos parentCanvasPos, double parentZoom)
    {
        var nodeCanvasPos = node.GetNodeCanvasPos(parentCanvasPos, parentZoom);

        if ((parentZoom <= MinContainerZoom || // Too small to show children
            node.Type == Parsing.NodeType.Member)  // Members do not have children
            && !node.IsRoot)                       // Root have icon but can have children
        {
            return GetIconSvg(nodeCanvasPos, parentZoom);   // No children can be seen
        }
        else
        {
            var containerSvg = GetContainerSvg(nodeCanvasPos, parentZoom);
            var childrenZoom = parentZoom * node.ContainerZoom;
            return node.AllItems()
                .Select(n => n.GetSvg(nodeCanvasPos, childrenZoom))
                .Prepend(containerSvg)
                .Join("");
        }
    }

    string GetIconSvg(Pos nodeCanvasPos, double zoom)
    {
        if (zoom > MaxNodeZoom) return "";  // To large to be seen

        var (x, y) = nodeCanvasPos;
        var (w, h) = (node.Boundary.Width * zoom, node.Boundary.Height * zoom);
        var s = node.StrokeWidth;

        var (tx, ty) = (x + w / 2, y + h);
        var fz = FontSize * zoom;
        var icon = GetIconSvg();

        return
            $"""
            <use href="#{icon}" x="{x}" y="{y}" width="{w}" height="{h}" />
            <text x="{tx}" y="{ty}" class="iconName" font-size="{fz}px">{node.ShortName}</text>
            <g class="hoverable">
              <rect x="{x - 2}" y="{y - 2}" width="{w + 2}" height="{h + 2}" stroke-width="1" rx="2" fill="black" fill-opacity="0" stroke="none"/>
            </g>
            </rect>
            """;
    }


    string GetContainerSvg(Pos nodeCanvasPos, double zoom)
    {
        if (node.IsRoot) return "";
        if (zoom > MaxNodeZoom) return "";  // To large to be seen

        var s = node.StrokeWidth;
        var (x, y) = nodeCanvasPos;
        var (w, h) = (node.Boundary.Width * zoom, node.Boundary.Height * zoom);
        var (ix, iy, iw, ih) = (x, y + h + 1 * zoom, SmallIconSize * zoom, SmallIconSize * zoom);

        var (tx, ty) = (x + (SmallIconSize + 1) * zoom, y + h + 2 * zoom);
        var fz = FontSize * zoom;
        var icon = GetIconSvg();

        return
            $"""
            <rect x="{x}" y="{y}" width="{w}" height="{h}" stroke-width="{s}" rx="5" fill="{node.Background}" fill-opacity="1" stroke="{node.StrokeColor}"/>      
            <use href="#{icon}" x="{ix}" y="{iy}" width="{iw}" height="{ih}" />
            <text x="{tx}" y="{ty}" class="nodeName" font-size="{fz}px">{node.ShortName}</text>
            <g class="hoverable">
              <rect x="{x - 2}" y="{y - 2}" width="{w + 2}" height="{h + 2}" stroke-width="1" rx="2" fill="black" fill-opacity="0" stroke="none"/>
            </g>
            """;
    }


    string GetIconSvg()
    {
        var icon = Icon.GetIconSvg(node.Type);
        return icon;
    }

}
