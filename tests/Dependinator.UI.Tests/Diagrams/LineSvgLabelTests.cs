using Dependinator.Core.Parsing;
using Dependinator.UI.Diagrams;
using Dependinator.UI.Diagrams.Cycles;
using Dependinator.UI.Diagrams.Interaction;
using Dependinator.UI.Diagrams.Navigation;
using Dependinator.UI.Diagrams.Rules;
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
        var line = new Line(source, target) { Visibility = NodeVisibility.Dimmed };
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
            new CycleService(modelMgr, Mock.Of<Dependinator.UI.Modeling.Commands.ICommandService>()),
            Mock.Of<IPathFinderService>(),
            Mock.Of<IRuleService>(),
            Mock.Of<IProblemService>(),
            Mock.Of<IHiddenNodeService>()
        );

        var svg = service.GetContentSvg(new Rect(0, 0, 2000, 2000), 1.0);

        Assert.Contains("class=\"lineLabel\"", svg);
        Assert.Contains(">uses</text>", svg);
        // The label layer comes after the nodes, so a short line's label is not under the icons.
        Assert.True(
            svg.IndexOf(">uses</text>", StringComparison.Ordinal) > svg.IndexOf(">B</text>", StringComparison.Ordinal)
        );
        // The line itself carries no second copy of the label.
        Assert.Equal(1, svg.Split("class=\"lineLabel\"").Length - 1);
    }

    [Fact]
    public void GetLineSvg_ShouldDrawTheDescription_UnderTheLabel()
    {
        var root = CreateRoot();
        var source = new Node("a", root) { Boundary = new Rect(0, 0, 100, 100), Type = NodeType.ClassType };
        var target = new Node("b", root) { Boundary = new Rect(300, 0, 100, 100), Type = NodeType.ClassType };
        root.AddChild(source);
        root.AddChild(target);
        var line = new Line(source, target);
        line.SetLabel("orders", "to the API");

        var svg = LineSvg.GetLineSvg(line, new Pos(0, 0), 1);

        Assert.Contains(">orders</text>", svg);
        Assert.Contains("class=\"lineDescription\"", svg);
        Assert.Contains(">to the API</tspan></text>", svg);
        var labelY = double.Parse(
            System
                .Text.RegularExpressions.Regex.Match(svg, "class=\"lineLabel\" x=\"[^\"]*\" y=\"([^\"]*)\"")
                .Groups[1]
                .Value,
            System.Globalization.CultureInfo.InvariantCulture
        );
        var descriptionY = double.Parse(
            System
                .Text.RegularExpressions.Regex.Match(svg, "class=\"lineDescription\" x=\"[^\"]*\" y=\"([^\"]*)\"")
                .Groups[1]
                .Value,
            System.Globalization.CultureInfo.InvariantCulture
        );
        Assert.True(descriptionY > labelY, "the description sits under the label");

        // A description alone is drawn too, on the line itself.
        line.SetLabel(null, "to the API");
        var only = LineSvg.GetLineSvg(line, new Pos(0, 0), 1);
        Assert.DoesNotContain("lineLabel", only);
        Assert.Contains(">to the API</tspan></text>", only);
    }

    [Fact]
    public void GetLineSvg_ShouldWrapTheDescription_KeepingTheUsersLineBreaks()
    {
        var line = CreateLine();
        line.SetLabel("orders", "Sent as JSON.\nRetried three times before the order is parked for a later run.");

        var rows = DescriptionRows(LineSvg.GetLineSvg(line, new Pos(0, 0), 1));

        // The user's break ends the first row; the long second row wraps at the width limit.
        Assert.Equal(["Sent as JSON.", "Retried three times before the order", "is parked for a later run."], rows);
        Assert.All(rows, row => Assert.True(row.Length <= LineSvg.LabelDescriptionMaxChars));
    }

    [Fact]
    public void GetLineSvg_ShouldLimitTheDescriptionRows_AndEndWithAnEllipsis()
    {
        var line = CreateLine();
        line.SetLabel("orders", "One.\nTwo.\nThree.\nFour.\nFive.");

        var rows = DescriptionRows(LineSvg.GetLineSvg(line, new Pos(0, 0), 1));

        Assert.Equal(["One.", "Two.", "Three…"], rows);
    }

    [Fact]
    public void GetUserTextLines_ShouldNotLeaveASpaceBeforeTheEllipsis()
    {
        var rows = SvgText.GetUserTextLines("One.\nTwo.\nThree :\nFour.", 38, 3);

        Assert.Equal(["One.", "Two.", "Three…"], rows);
    }

    [Fact]
    public void GetLineSvg_ShouldClipALongLabel_ToOneRow()
    {
        var line = CreateLine();
        line.SetLabel("a label that is much too long to be drawn on a line in full");

        var svg = LineSvg.GetLineSvg(line, new Pos(0, 0), 1);

        var text = System.Text.RegularExpressions.Regex.Match(svg, "class=\"lineLabel\"[^>]*>([^<]*)<").Groups[1].Value;
        Assert.Equal(LineSvg.LabelMaxChars, text.Length);
        Assert.StartsWith("a label that is much too long", text);
        Assert.EndsWith("…", text);
    }

    [Fact]
    public void GetLineSvg_ShouldDrawLabelsSmallerThanNodeNames_AndStopGrowingEarlier()
    {
        var line = CreateLine();
        line.SetLabel("orders", "to the API");

        Assert.True(FontSize(LineSvg.GetLineSvg(line, new Pos(0, 0), 1), "lineLabel") < SvgText.FontSize);
        // Zoomed far in, the label keeps the size it had at LabelMaxTextZoom.
        Assert.Equal(
            LineSvg.LabelFontSize * LineSvg.LabelMaxTextZoom,
            FontSize(LineSvg.GetLineSvg(line, new Pos(0, 0), 10), "lineLabel"),
            2
        );
        Assert.Equal(
            LineSvg.LabelDescriptionFontSize * LineSvg.LabelMaxTextZoom,
            FontSize(LineSvg.GetLineSvg(line, new Pos(0, 0), 10), "lineDescription"),
            2
        );
    }

    static Line CreateLine()
    {
        var root = CreateRoot();
        var source = new Node("a", root) { Boundary = new Rect(0, 0, 100, 100), Type = NodeType.ClassType };
        var target = new Node("b", root) { Boundary = new Rect(300, 0, 100, 100), Type = NodeType.ClassType };
        root.AddChild(source);
        root.AddChild(target);
        return new Line(source, target);
    }

    static List<string> DescriptionRows(string svg)
    {
        var text = System.Text.RegularExpressions.Regex.Match(svg, "<text class=\"lineDescription\".*?</text>").Value;
        return System
            .Text.RegularExpressions.Regex.Matches(text, "<tspan[^>]*>([^<]*)</tspan>")
            .Select(m => System.Web.HttpUtility.HtmlDecode(m.Groups[1].Value))
            .ToList();
    }

    static double FontSize(string svg, string cssClass) =>
        double.Parse(
            System
                .Text.RegularExpressions.Regex.Match(svg, $"class=\"{cssClass}\"[^>]*font-size=\"([0-9.]+)px\"")
                .Groups[1]
                .Value,
            System.Globalization.CultureInfo.InvariantCulture
        );

    [Fact]
    public void GetLineSvg_ShouldDrawTheLabel_WhereTheUserMovedIt()
    {
        var line = CreateLine();
        line.SetLabel("orders");
        line.SetLabelPosition(0.25);

        var svg = LineSvg.GetLineSvg(line, new Pos(0, 0), 1);

        // A quarter of the way from the right edge of a (100) to the left edge of b (300).
        Assert.Matches("class=\"lineLabel\" x=\"150\"", svg);
    }

    [Fact]
    public void GetLineLabelSvg_ShouldGiveTheLabelAHitTarget_ThatIsItsLine()
    {
        var line = CreateLine();
        line.SetLabel("orders", "to the API");

        var svg = LineSvg.GetLineLabelSvg(line, new Pos(0, 0), 1, isDimmed: false);

        // The group carries the line's id (the hover card), the rect the label's own (the drag).
        Assert.Contains($"<g class=\"hoverable\" id=\"{PointerId.FromLine(line.Id).ElementId}\">", svg);
        Assert.Contains($"<rect id=\"{PointerId.LineLabelElementId(line.Id)}\"", svg);
        Assert.Contains(LineSvg.LabelHitClass, svg);
        Assert.DoesNotContain("pointer-events=\"none\" /></g>", svg);

        // On a selected line the label lets presses through to a bend point's handle under it,
        // and keeps them where no handle is near (a double-click there still edits the label).
        line.IsSelected = true;
        line.SetSegmentPoints([new Pos(200, 50)]);
        Assert.Contains(
            "pointer-events=\"none\" /></g>",
            LineSvg.GetLineLabelSvg(line, new Pos(0, 0), 1, isDimmed: false)
        );
        line.SetSegmentPoints([new Pos(150, 80)]);
        Assert.DoesNotContain(
            "pointer-events=\"none\" /></g>",
            LineSvg.GetLineLabelSvg(line, new Pos(0, 0), 1, isDimmed: false)
        );
    }
}
