using Dependinator.Core.Parsing;
using Dependinator.UI.Diagrams;
using Dependinator.UI.Diagrams.Svg;
using Dependinator.UI.Diagrams.Tiles;
using Dependinator.UI.Modeling;
using Dependinator.UI.Modeling.Models;
using Dependinator.UI.Shared;
using Dependinator.UI.Shared.Types;
using Link = Dependinator.UI.Modeling.Models.Link;
using Node = Dependinator.UI.Modeling.Models.Node;

namespace Dependinator.UI.Tests.Diagrams;

// A line's user label is drawn at the line's middle inside the line's visible group, so it dims
// and fades with the line; without a label nothing is drawn.
public class LineSvgLabelTests
{
    static Node CreateRoot() => new("", null!) { Type = NodeType.Root, ContainerZoom = 1 };

    [Fact]
    public void GetLineSvg_ShouldDrawTheLabel_AtTheMiddleOfTheLine()
    {
        var root = CreateRoot();
        var source = new Node("a", root) { Boundary = new Rect(0, 0, 100, 100), Type = NodeType.ClassType };
        var target = new Node("b", root) { Boundary = new Rect(300, 0, 100, 100), Type = NodeType.ClassType };
        root.AddChild(source);
        root.AddChild(target);
        var line = new Line(source, target);
        line.SetLabel("orders & <payments>");

        var svg = LineSvg.GetLineSvg(line, new Pos(0, 0), 1);

        Assert.Contains("class=\"lineLabel\"", svg);
        Assert.Contains(">orders &amp; &lt;payments&gt;</text>", svg);
        // Inside the visible group (before the hover group), so line-dim applies to it.
        Assert.True(
            svg.IndexOf("lineLabel", StringComparison.Ordinal)
                < svg.IndexOf("class=\"hoverable\"", StringComparison.Ordinal)
        );
        // Halfway between the right edge of a (100) and the left edge of b (300).
        Assert.Contains("x=\"200\"", svg);
        Assert.DoesNotContain("opacity=\"0.3\"", svg);

        line.SetLabel(null);
        Assert.DoesNotContain("lineLabel", LineSvg.GetLineSvg(line, new Pos(0, 0), 1));
    }

    [Fact]
    public void GetLineSvg_ShouldFadeTheLabel_OfAHiddenLine()
    {
        var root = CreateRoot();
        var source = new Node("a", root) { Boundary = new Rect(0, 0, 100, 100), Type = NodeType.ClassType };
        var target = new Node("b", root) { Boundary = new Rect(300, 0, 100, 100), Type = NodeType.ClassType };
        root.AddChild(source);
        root.AddChild(target);
        var line = new Line(source, target) { IsHidden = true };
        line.SetLabel("uses");

        var svg = LineSvg.GetLineSvg(line, new Pos(0, 0), 1);

        Assert.Contains("lineLabel", svg);
        Assert.Contains("opacity=\"0.3\"", svg);
    }

    [Fact]
    public void GetContentSvg_ShouldRenderTheLabel_WithTheLine()
    {
        var modelMgr = new ModelMgr(new StateMgr());
        using (var model = modelMgr.UseModel())
        {
            var a = new Node("A", model.Root) { Boundary = new Rect(0, 0, 100, 100), Type = NodeType.ClassType };
            var b = new Node("B", model.Root) { Boundary = new Rect(300, 0, 100, 100), Type = NodeType.ClassType };
            model.Root.AddChild(a);
            model.Root.AddChild(b);
            model.TryAddNode(a);
            model.TryAddNode(b);
            var link = new Link(a, b);
            model.TryAddLink(link);
            a.AddSourceLink(link);
            b.AddTargetLink(link);
            new LineService().AddLinesFromSourceToTarget(model, link);
            model.Lines[LineId.From("A", "B")].SetLabel("uses");
        }
        ISvgService service = new SvgService(
            modelMgr,
            Mock.Of<ITilesMgr>(),
            new ViewOptions(),
            new CycleService(modelMgr),
            Mock.Of<IPathFinderService>(),
            Mock.Of<IRuleService>()
        );

        var svg = service.GetContentSvg(new Rect(0, 0, 2000, 2000), 1.0);

        Assert.Contains("class=\"lineLabel\"", svg);
        Assert.Contains(">uses</text>", svg);
    }
}
