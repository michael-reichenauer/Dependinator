using Dependinator.Core.Parsing;
using Dependinator.UI.Diagrams.Interaction;
using Dependinator.UI.Diagrams.Svg;
using Dependinator.UI.Modeling.Models;
using Dependinator.UI.Shared.Types;
using Node = Dependinator.UI.Modeling.Models.Node;

namespace Dependinator.UI.Tests.Diagrams;

// A selected line in edit mode shows a hollow handle on each of its segments, which a drag
// turns into a bend point (PointerId.FromLineMidPoint).
public class LineSvgSegmentHandleTests
{
    // A selected line from a's right edge (100, 50) to b's left edge (300, 50).
    static Line CreateSelectedLine(double targetX = 300)
    {
        var root = new Node("", null!) { Type = NodeType.Root, ContainerZoom = 1 };
        var a = new Node("a", root) { Boundary = new Rect(0, 0, 100, 100), Type = NodeType.ClassType };
        var b = new Node("b", root) { Boundary = new Rect(targetX, 0, 100, 100), Type = NodeType.ClassType };
        root.AddChild(a);
        root.AddChild(b);
        return new Line(a, b) { IsSelected = true };
    }

    static string HandleId(Line line, int segment, int percent = 50) =>
        PointerId.FromLineMidPoint(line.Id, segment, percent).ElementId;

    [Fact]
    public void GetLineSvg_ShouldDrawASegmentHandle_InTheMiddleOfEachSegment_InEditMode()
    {
        var line = CreateSelectedLine();
        line.SetSegmentPoints([new Pos(200, 150)]);

        var svg = LineSvg.GetLineSvg(line, new Pos(0, 0), 1, isEditing: true);

        Assert.Contains($"id=\"{HandleId(line, 0)}\" cx=\"150\" cy=\"100\"", svg);
        Assert.Contains($"id=\"{HandleId(line, 1)}\" cx=\"250\" cy=\"100\"", svg);
        Assert.Contains("segment-handle", svg);
    }

    [Fact]
    public void GetLineSvg_ShouldDrawNoSegmentHandles_InViewMode_OrForAnUnselectedLine()
    {
        var line = CreateSelectedLine();

        Assert.DoesNotContain("segment-handle", LineSvg.GetLineSvg(line, new Pos(0, 0), 1));

        line.IsSelected = false;
        Assert.DoesNotContain("segment-handle", LineSvg.GetLineSvg(line, new Pos(0, 0), 1, isEditing: true));
    }

    [Fact]
    public void GetLineSvg_ShouldDrawNoSegmentHandle_OnATooShortSegment()
    {
        var line = CreateSelectedLine(targetX: 130); // 30 px long

        Assert.DoesNotContain("segment-handle", LineSvg.GetLineSvg(line, new Pos(0, 0), 1, isEditing: true));
    }

    [Fact]
    public void GetLineSvg_ShouldMoveTheSegmentHandle_OutFromUnderTheLabel()
    {
        var line = CreateSelectedLine();
        line.SetLabel("uses");

        var svg = LineSvg.GetLineSvg(line, new Pos(0, 0), 1, isEditing: true);

        Assert.DoesNotContain(HandleId(line, 0), svg);
        Assert.Contains($"id=\"{HandleId(line, 0, 25)}\" cx=\"150\" cy=\"50\"", svg);
    }
}
