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

    static bool IsToLargeToBeSeen(double zoom) => zoom > MaxNodeZoom;
    static bool IsToSmallToShowChildren(double zoom) => zoom <= MinContainerZoom;

    public bool IsShowingChildren(double zoom) =>
        node.Children.Any() && !IsToSmallToShowChildren(zoom);


    public string GetSvg(Pos parentCanvasPos, double parentZoom)
    {
        if (node.IsRoot) return GetChildrenSvg(parentCanvasPos, parentZoom);

        if (IsToLargeToBeSeen(parentZoom)) return "";

        if (!IsShowingChildren(parentZoom)) return GetIconSvg(parentCanvasPos, parentZoom);

        return GetContainerSvg(parentCanvasPos, parentZoom) +
            GetChildrenSvg(parentCanvasPos, parentZoom);
    }

    string GetChildrenSvg(Pos parentCanvasPos, double parentZoom)
    {
        var nodeCanvasPos = node.GetNodeCanvasPos(parentCanvasPos, parentZoom);

        var childrenZoom = parentZoom * node.ContainerZoom;
        return node.AllItems()
            .Select(n => n.GetSvg(nodeCanvasPos, childrenZoom))
            .Join("");
    }

    string GetIconSvg(Pos parentCanvasPos, double parentZoom)
    {
        var nodeCanvasPos = node.GetNodeCanvasPos(parentCanvasPos, parentZoom);

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
              <rect x="{x - 2}" y="{y - 2}" width="{w + 2}" height="{h + 2}" stroke-width="1" rx="2" fill="black" fill-opacity="0" stroke="none"/>
            </g>
            </rect>
            """;
    }


    string GetContainerSvg(Pos parentCanvasPos, double parentZoom)
    {
        var nodeCanvasPos = node.GetNodeCanvasPos(parentCanvasPos, parentZoom);

        var s = node.StrokeWidth;
        var (x, y) = nodeCanvasPos;
        var (w, h) = (node.Boundary.Width * parentZoom, node.Boundary.Height * parentZoom);
        var (ix, iy, iw, ih) = (x, y + h + 1 * parentZoom, SmallIconSize * parentZoom, SmallIconSize * parentZoom);

        var (tx, ty) = (x + (SmallIconSize + 1) * parentZoom, y + h + 2 * parentZoom);
        var fz = FontSize * parentZoom;
        var icon = node.Type.IconName;

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
}
