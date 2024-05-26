namespace Dependinator.Models;


interface ISvgService
{
  Tile GetTile(IModel model, Rect viewRect, double zoom);
}


[Transient]
class SvgService : ISvgService
{
  const int SmallIconSize = 9;
  const int FontSize = 8;


  public Tile GetTile(IModel model, Rect viewRect, double zoom)
  {
    // Log.Info($"GetSvg: {viewRect} zoom: {zoom}");
    if (!model.Root.Children.Any()) return Tile.Empty;

    var tileKey = TileKey.From(viewRect, zoom);
    if (model.Tiles.TryGetCached(tileKey, out var tile)) return tile;

    tile = GetModelTile(model, tileKey);
    model.Tiles.SetCached(tile);

    // Log.Info("+++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++");
    // Log.Info($"Tile: K:{tile.Key}, O: {tile.Offset}, Z: {tile.Zoom}, svg: {tile.Svg.Length} chars, Tiles: {model.Tiles}");
    return tile;
  }

  Tile GetModelTile(IModel model, TileKey tileKey)
  {
    // using var t = Timing.Start($"GetModelSvg: {tileKey}");
    var tileRect = tileKey.GetTileRect();
    var tileWithMargin = tileKey.GetTileRectWithMargin();
    var tileZoom = tileKey.GetTileZoom();

    var tileOffset = new Pos(-tileRect.X, -tileRect.Y);
    var svgContent = GetNodeContentSvg(model.Root, tileOffset, 1 / tileZoom, tileWithMargin);

    var (x, y, w, h) = tileKey.GetViewRect();

    var tileViewBox = $"{x} {y} {w} {h}";
    var tileSvg = $"""<svg width="{w}" height="{h}" viewBox="{tileViewBox}" xmlns="http://www.w3.org/2000/svg">{svgContent}</svg>""";

    return new Tile(tileKey, tileSvg, tileZoom, tileOffset);
  }

  static string GetNodeContentSvg(Node node, Pos nodeCanvasPos, double zoom, Rect tileWithMargin)
  {
    if (node.IsChildrenLayoutRequired) NodeLayout.AdjustChildren(node);

    var childrenZoom = zoom * node.ContainerZoom;

    var childrenPos = new Pos(nodeCanvasPos.X + node.ContainerOffset.X * zoom, nodeCanvasPos.Y + node.ContainerOffset.Y * zoom);

    return node.Children
        .Select(n => GetNodeSvg(n, childrenPos, childrenZoom, tileWithMargin))
        .Concat(GetNodeLinesSvg(node, childrenPos, zoom, childrenZoom))
        .Join("\n");
  }

  static string GetNodeSvg(Node node, Pos offset, double zoom, Rect tileWithMargin)
  {
    var nodeCanvasPos = GetNodeCanvasPos(node, offset, zoom);
    var nodeCanvasRect = GetNodeCanvasRect(node, nodeCanvasPos, zoom);

    //if (!IsOverlap(tileWithMargin, nodeCanvasRect)) return ""; // Outside the tile limit

    if (Node.IsToLargeToBeSeen(zoom)) return GetNodeContentSvg(node, nodeCanvasPos, zoom, tileWithMargin);

    if (node.IsShowIcon(zoom)) return GetNodeIconSvg(node, nodeCanvasRect, zoom);

    return
        GetNodeContainerSvg(node, nodeCanvasRect, zoom, GetNodeContentSvg(node, Pos.Zero, zoom, tileWithMargin));
  }


  static IEnumerable<string> GetNodeLinesSvg(Node node, Pos nodeCanvasPos, double parentZoom, double childrenZoom)
  {
    var parentToChildrenLines = node.SourceLines.Where(l => l.Target.Parent == node);
    foreach (var line in parentToChildrenLines)
    {
      yield return GetLineSvg(line, nodeCanvasPos, parentZoom, childrenZoom);
    }

    // All sibling lines and children to parent lines
    foreach (var child in node.Children)
    {
      foreach (var line in child.SourceLines)
      {
        if (line.Target.Parent == line.Source) continue;
        yield return GetLineSvg(line, nodeCanvasPos, parentZoom, childrenZoom);
      }
    }
  }



  static Pos GetNodeCanvasPos(Node node, Pos offset, double zoom) => new(
     offset.X + node.Boundary.X * zoom, offset.Y + node.Boundary.Y * zoom);

  static Rect GetNodeCanvasRect(Node node, Pos nodeCanvasPos, double zoom) => new(
     nodeCanvasPos.X, nodeCanvasPos.Y, node.Boundary.Width * zoom, node.Boundary.Height * zoom);


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


  static string GetNodeIconSvg(Node node, Rect parentCanvasRect, double parentZoom)
  {
    var (x, y) = (parentCanvasRect.X, parentCanvasRect.Y);
    var (w, h) = (node.Boundary.Width * parentZoom, node.Boundary.Height * parentZoom);

    var (tx, ty) = (x + w / 2, y + h);
    var fz = FontSize * parentZoom;
    var icon = node.Type.IconName;
    //Log.Info($"Icon: {node.LongName} ({x},{y},{w},{h}) ,{node.Boundary}, Z: {parentZoom}");
    //var toolTip = $"{node.HtmlLongName}, np: {node.Boundary}, Zoom: {parentZoom}, cr: {x}, {y}, {w}, {h}";
    string selectedSvg = SelectedNodeSvg(node, x, y, w, h);

    return
        $"""
            <use href="#{icon}" x="{x:0.##}" y="{y:0.##}" width="{w:0.##}" height="{h:0.##}" />
            <text x="{tx:0.##}" y="{ty:0.##}" class="iconName" font-size="{fz:0.##}px">{node.HtmlShortName}</text>
            <g class="hoverable" id="{node.Id.Value}">
              <rect id="{node.Id.Value}.i" x="{x:0.##}" y="{y:0.##}" width="{w:0.##}" height="{h:0.##}" stroke-width="1" rx="2" fill="black" fill-opacity="0" stroke="none"/>
              <title>{node.HtmlLongName}</title>
            </g>
            {selectedSvg}
            """;
  }


  static string GetNodeContainerSvg(Node node, Rect parentCanvasRect, double parentZoom, string childrenContent)
  {
    var s = node.IsEditMode ? 10 : node.StrokeWidth;
    var (x, y) = (parentCanvasRect.X, parentCanvasRect.Y);
    var (w, h) = (node.Boundary.Width * parentZoom, node.Boundary.Height * parentZoom);
    var iSize = SmallIconSize * parentZoom;
    var (ix, iy, iw, ih) = (x, y + h + 1 * parentZoom, iSize, iSize);

    var (tx, ty) = (x + (SmallIconSize + 1) * parentZoom, y + h + 2 * parentZoom);
    var fz = FontSize * parentZoom;
    var icon = node.Type.IconName;

    //Log.Info($"Container: {node.LongName} ({x},{y},{w},{h}) ,{node.Boundary}, Z: {parentZoom}");
    //var toolTip = $"{node.HtmlLongName}, np: {node.Boundarsy}, Zoom: {parentZoom}, cr: {x}, {y}, {w}, {h}";

    string selectedSvg = SelectedNodeSvg(node, x, y, w, h);

    var cl = node.IsEditMode ? "hoverableedit" : "hoverable";
    var c = node.IsEditMode ? Coloring.EditNode : node.Color;
    var back = node.IsEditMode ? Coloring.EditNodeBack : node.Background;

    return
        $"""
            <svg x="{x:0.##}" y="{y:0.##}" width="{w:0.##}" height="{h:0.##}" viewBox="{0} {0} {w:0.##} {h:0.##}" xmlns="http://www.w3.org/2000/svg">
              <rect x="{0}" y="{0}" width="{w:0.##}" height="{h:0.##}" stroke-width="{s}" rx="5" fill="{back}" stroke="{c}"/>
              <g class="{cl}" id="{node.Id.Value}">
                <rect id="{node.Id.Value}.c" x="{0}" y="{0}" width="{w:0.##}" height="{h:0.##}" stroke-width="1" rx="2" fill="black" fill-opacity="0" stroke="none"/>
                <title>{node.HtmlLongName}</title>
              </g>
              {childrenContent}
            </svg>
            <use href="#{icon}" x="{ix:0.##}" y="{iy:0.##}" width="{iw:0.##}" height="{ih:0.##}" />
            <text x="{tx:0.##}" y="{ty:0.##}" class="nodeName" font-size="{fz:0.##}px">{node.HtmlShortName}</text>
            {selectedSvg}
            """;
  }

  static string SelectedNodeSvg(Node node, double x, double y, double w, double h)
  {
    if (!node.IsSelected) return "";

    return SelectedResizeSvg(node, x, y, w, h);
  }


  static string SelectedResizeSvg(Node node, double x, double y, double w, double h)
  {
    string c = Coloring.Highlight;
    const int s = 8;
    const int m = 3;
    const int mt = m + s;
    const int ml = m + s;
    const int mm = s / 2;
    const int mr = m;
    const int mb = m;

    const int tt = 12;
    const int t = 10 * 3 + 1;

    return
        $"""
        <g class="selectpoint">
          <rect id="{node.Id.Value}.tl" x="{x - ml}" y="{y - mt}" width="{s}" height="{s}" fill="{c}" />
          <rect id="{node.Id.Value}.tl" x="{x - ml - tt}" y="{y - mt - tt}" rx="20" width="{t}" height="{t}" fill="{c}" fill-opacity="0"/>
        </g>
        <g class="selectpoint">
          <rect id="{node.Id.Value}.tm" x="{x + w / 2 - mm}" y="{y - mt}" width="{s}" height="{s}" fill="{c}" />
          <rect id="{node.Id.Value}.tm" x="{x + w / 2 - mm - tt}" y="{y - mt - tt}" rx="20" width="{t}" height="{t}" fill="{c}" fill-opacity="0" />
        </g>
        <g class="selectpoint">
          <rect id="{node.Id.Value}.tr" x="{x + w + mr}" y="{y - mt}" width="{s}" height="{s}" fill="{c}" />
          <rect id="{node.Id.Value}.tr" x="{x + w + mr - tt}" y="{y - mt - tt}" rx="20" width="{t}" height="{t}" fill="{c}" fill-opacity="0"/>
        </g>
        <g class="selectpoint">
          <rect id="{node.Id.Value}.ml" x="{x - ml}" y="{y + h / 2}" width="{s}" height="{s}" fill="{c}" />
          <rect id="{node.Id.Value}.ml" x="{x - ml - tt}" y="{y + h / 2 - tt}" rx="20" width="{t}" height="{t}" fill="{c}" fill-opacity="0"/>
        </g>
        <g class="selectpoint">
          <rect id="{node.Id.Value}.mr" x="{x + w + mr}" y="{y + h / 2}" width="{s}" height="{s}" fill="{c}" />
          <rect id="{node.Id.Value}.mr" x="{x + w + mr - tt}" y="{y + h / 2 - tt}" rx="20" width="{t}" height="{t}" fill="{c}" fill-opacity="0"/>
        </g>
        <g class="selectpoint">    
          <rect id="{node.Id.Value}.bl" x="{x - ml}" y="{y + h + mb}" width="{s}" height="{s}" fill="{c}" />
          <rect id="{node.Id.Value}.bl" x="{x - ml - tt}" y="{y + h + mb - tt}" rx="20" width="{t}" height="{t}" fill="{c}" fill-opacity="0"/>
        </g>
        <g class="selectpoint">
          <rect id="{node.Id.Value}.bm" x="{x + w / 2 - mm}" y="{y + h + mb}" width="{s}" height="{s}" fill="{c}" />
          <rect id="{node.Id.Value}.bm" x="{x + w / 2 - mm - tt}" y="{y + h + mb - tt}" rx="20" width="{t}" height="{t}" fill="{c}" fill-opacity="0" />
        </g>
        <g class="selectpoint">
          <rect id="{node.Id.Value}.br" x="{x + w + mr}" y="{y + h + mb}" width="{s}" height="{s}" fill="{c}" />
          <rect id="{node.Id.Value}.br" x="{x + w + mr - tt}" y="{y + h + mb - tt}" rx="20" width="{t}" height="{t}" fill="{c}" fill-opacity="0"/>
        </g>
        """;
  }



  static string GetLineSvg(Line line, Pos nodeCanvasPos, double parentZoom, double childrenZoom)
  {
    if (Node.IsToLargeToBeSeen(childrenZoom)) return "";

    var sw = line.StrokeWidth;
    var (s, t) = (line.Source.Boundary, line.Target.Boundary);

    var (x1, y1) = (s.X + s.Width, s.Y + s.Height / 2);
    var (x2, y2) = (t.X, t.Y + t.Height / 2);

    if (line.Target.Parent == line.Source)
    {   // Parent source to child target (left of parent to right of child)
      if (Node.IsToLargeToBeSeen(parentZoom)) return "";
      var parent = line.Source;
      (x1, y1) = (nodeCanvasPos.X - parent.ContainerOffset.X * parentZoom,
        nodeCanvasPos.Y + s.Height / 2 * parentZoom - parent.ContainerOffset.Y * parentZoom);
      (x2, y2) = (nodeCanvasPos.X + x2 * childrenZoom, nodeCanvasPos.Y + y2 * childrenZoom);
    }
    else if (line.Source.Parent == line.Target)
    {   // Child source to parent target (left of child to right of parent)
      if (Node.IsToLargeToBeSeen(parentZoom)) return "";
      var parent = line.Target;
      (x1, y1) = (nodeCanvasPos.X + x1 * childrenZoom, nodeCanvasPos.Y + y1 * childrenZoom);

      (x2, y2) = (nodeCanvasPos.X + t.Width * parentZoom - parent.ContainerOffset.X * parentZoom,
      nodeCanvasPos.Y + t.Height / 2 * parentZoom - parent.ContainerOffset.Y * parentZoom);
    }
    else
    {   // Sibling source to sibling target (right of source to left of target)
      (x1, y1) = (nodeCanvasPos.X + x1 * childrenZoom, nodeCanvasPos.Y + y1 * childrenZoom);
      (x2, y2) = (nodeCanvasPos.X + x2 * childrenZoom, nodeCanvasPos.Y + y2 * childrenZoom);
    }

    var c = "#B388FF";
    return
        $"""
            <line x1="{x1}" y1="{y1}" x2="{x2}" y2="{y2}" stroke-width="{sw}" stroke="{c}" marker-end="url(#arrow)" />
            <g class="hoverable" >
              <line x1="{x1}" y1="{y1}" x2="{x2}" y2="{y2}" stroke-width="{sw + 10}" stroke="black" stroke-opacity="0" />
              <title>{line.Source.HtmlLongName} {line.Target.HtmlLongName}</title>
            </g>
            """;
  }
}
