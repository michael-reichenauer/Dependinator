namespace Dependinator.Models;

class NodeSvg
{
    const int SmallIconSize = 9;
    const int FontSize = 8;

    readonly Node node;

    public NodeSvg(Node node)
    {
        this.node = node;
    }


    public string GetSvg(Pos parentCanvasPos, double zoom)
    {
        var nodeCanvasPos = node.GetNodeCanvasPos(parentCanvasPos, zoom);

        if (node.IsRoot || Node.IsToLargeToBeSeen(zoom))
            return GetChildrenSvg(nodeCanvasPos, zoom);

        if (!node.IsShowingChildren(zoom)) return GetIconSvg(nodeCanvasPos, zoom);

        return GetContainerSvg(nodeCanvasPos, zoom) +
            GetChildrenSvg(nodeCanvasPos, zoom);
    }

    string GetChildrenSvg(Pos nodeCanvasPos, double zoom)
    {
        var childrenZoom = zoom * node.ContainerZoom;
        return node.AllItems()
            .Select(n => n.GetSvg(nodeCanvasPos, childrenZoom))
            .Join("");
    }

    string GetIconSvg(Pos nodeCanvasPos, double parentZoom)
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


    string GetContainerSvg(Pos nodeCanvasPos, double parentZoom)
    {
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
              <rect id="{node.Id.Value}" x="{x - 2}" y="{y - 2}" width="{w + 2}" height="{h + 2}" stroke-width="1" rx="2" fill="black" fill-opacity="0" stroke="none"/>
            </g>
            """;
    }
}
