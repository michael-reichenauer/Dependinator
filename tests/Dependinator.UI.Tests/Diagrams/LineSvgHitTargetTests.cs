using System.Xml.Linq;
using Dependinator.Core.Parsing;
using Dependinator.UI.Diagrams.Svg;
using Dependinator.UI.Modeling.Models;
using Dependinator.UI.Shared.Types;
using Node = Dependinator.UI.Modeling.Models.Node;

namespace Dependinator.UI.Tests.Diagrams;

// A line's hover group holds the wide hit polyline, which carries the line's id and which the
// .hoverable:hover rule skips, apart from the highlight polyline that the rule strokes. Hover
// restyling the hit polyline shrank it under the pointer, and WebKit clicks then missed the line.
// Nothing drawn above the hit polyline may take a press either (the selection overlay).
public class LineSvgHitTargetTests
{
    static Line CreateLine()
    {
        var root = new Node("", null!) { Type = NodeType.Root, ContainerZoom = 1 };
        var source = new Node("a", root) { Boundary = new Rect(0, 0, 100, 100), Type = NodeType.ClassType };
        var target = new Node("b", root) { Boundary = new Rect(300, 0, 100, 100), Type = NodeType.ClassType };
        root.AddChild(source);
        root.AddChild(target);
        return new Line(source, target);
    }

    static XElement Render(Line line) => XElement.Parse($"<svg>{LineSvg.GetLineSvg(line, new Pos(0, 0), 1)}</svg>");

    [Fact]
    public void GetLineSvg_ShouldKeepTheHitTarget_ApartFromTheHoverHighlight()
    {
        var svg = Render(CreateLine());
        var group = svg.Descendants("g").Single(g => (string?)g.Attribute("class") == "hoverable");
        var polylines = group.Elements("polyline").ToList();
        Assert.Equal(2, polylines.Count);

        var hit = polylines.Single(p => (string?)p.Attribute("class") == LineSvg.HitClass);
        Assert.Equal((string?)group.Attribute("id"), (string?)hit.Attribute("id"));
        Assert.Equal("0", (string?)hit.Attribute("stroke-opacity"));

        // Never a click target (no id, no pointer events) and unstroked until hovered.
        var highlight = polylines.Single(p => (string?)p.Attribute("class") == LineSvg.HoverClass);
        Assert.Null(highlight.Attribute("id"));
        Assert.Null(highlight.Attribute("stroke"));
        Assert.Equal("none", (string?)highlight.Attribute("pointer-events"));
        Assert.Equal("none", (string?)highlight.Attribute("fill"));
        // Along the visible path, which an inheritance line trims out of its hollow arrow head.
        var visible = svg.Descendants("g")
            .Single(g => (string?)g.Attribute("class") == "line-vis")
            .Element("polyline")!;
        Assert.Equal((string?)visible.Attribute("points"), (string?)highlight.Attribute("points"));
    }

    [Fact]
    public void GetLineSvg_ShouldLetPressesThroughTheSelectionOverlay()
    {
        var line = CreateLine();
        line.IsSelected = true;

        var svg = Render(line);

        // The overlay is drawn above the hit polyline; an element without an id that took the
        // press would click "nothing" and deselect the line.
        var lineGroup = svg.Element("g")!;
        var overlay = lineGroup.Elements().Where(e => e.Name != "g").ToList();
        Assert.NotEmpty(overlay);
        Assert.All(overlay, e => Assert.Equal("none", (string?)e.Attribute("pointer-events")));
    }
}
