using System.Xml.Linq;
using Dependinator.Core.Parsing;
using Dependinator.UI.Diagrams.Svg;
using Dependinator.UI.Modeling.Models;
using Dependinator.UI.Shared.Types;
using Node = Dependinator.UI.Modeling.Models.Node;

namespace Dependinator.UI.Tests.Diagrams;

// Soft lines are CSS only (Canvas.razor.cs LineCss): the line's visible group carries the classes
// that keep it at full strength (line-lit for the selection's own lines) and data-n with its two
// end node ids, which the hover preview (jsInterop.js) matches without a redraw.
public class LineSvgSoftLinesTests
{
    static Node CreateRoot() => new("", null!) { Type = NodeType.Root, ContainerZoom = 1 };

    static Node AddNode(Node parent, string name, double x)
    {
        var node = new Node(name, parent) { Type = NodeType.ClassType, Boundary = new Rect(x, 0, 100, 100) };
        parent.AddChild(node);
        return node;
    }

    static XElement VisibleGroup(string svg) =>
        XElement.Parse($"<svg>{svg}</svg>").Descendants("g").First(g => ClassOf(g).Contains("line-vis"));

    static string ClassOf(XElement element) => (string?)element.Attribute("class") ?? "";

    [Fact]
    public void GetLineSvg_ShouldNameTheEndNodes_AndMarkLitLines()
    {
        var root = CreateRoot();
        var a = AddNode(root, "A", 0);
        var b = AddNode(root, "B", 300);
        var line = new Line(a, b);

        var plain = VisibleGroup(LineSvg.GetLineSvg(line, new Pos(0, 0), 1));
        var lit = VisibleGroup(LineSvg.GetLineSvg(line, new Pos(0, 0), 1, isLit: true));

        Assert.Equal($"{a.Id.Value} {b.Id.Value}", (string?)plain.Attribute("data-n"));
        Assert.DoesNotContain("line-lit", ClassOf(plain));
        Assert.Contains("line-lit", ClassOf(lit));
    }

    [Fact]
    public void IsLineLit_ShouldLightOnlyTheSelectionsOwnLines()
    {
        var root = CreateRoot();
        var a = AddNode(root, "A", 0);
        var b = AddNode(root, "B", 300);
        var c = AddNode(root, "C", 600);
        var ab = new Line(a, b);
        var bc = new Line(b, c);

        var selection = new SvgService.RenderSelection(a, null);

        Assert.True(SvgService.IsLineLit(ab, selection));
        Assert.False(SvgService.IsLineLit(bc, selection));
        Assert.False(SvgService.IsLineLit(ab, new SvgService.RenderSelection(null, null)));
    }
}
