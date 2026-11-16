using Dependinator.Core.Parsing;
using Dependinator.UI.Diagrams.Lines;
using Dependinator.UI.Diagrams.Svg;
using Dependinator.UI.Modeling.Models;
using Dependinator.UI.Shared.Types;
using Node = Dependinator.UI.Modeling.Models.Node;

namespace Dependinator.UI.Tests.Diagrams;

// A label the user has not dragged finds a place along its line clear of the container's nodes
// and of the other labels.
public class LabelPlacementTests
{
    static Node CreateRoot() => new("", null!) { Type = NodeType.Root, ContainerZoom = 1 };

    static Node AddNode(Node parent, string name, Rect boundary)
    {
        var node = new Node(name, parent) { Boundary = boundary, Type = NodeType.ClassType };
        parent.AddChild(node);
        return node;
    }

    static Line AddLine(Node source, Node target, string label)
    {
        var line = new Line(source, target) { IsActiveRep = true };
        line.SetLabel(label);
        source.SourceLines.Add(line);
        return line;
    }

    static Rect LabelBox(Line line)
    {
        var points = LinePathGeometry.GetLocalPolylinePoints(line);
        var at = LinePathGeometry.PointAt(points, line.LabelPosition);
        var box = LineSvg.GetLocalLabelBoxAroundPoint(line, points);
        return new Rect(at.X + box.X, at.Y + box.Y, box.Width, box.Height);
    }

    static bool Overlaps(Rect a, Rect b) =>
        a.X < b.X + b.Width && b.X < a.X + a.Width && a.Y < b.Y + b.Height && b.Y < a.Y + a.Height;

    [Fact]
    public void Place_ShouldKeepTheLabelInTheMiddle_WhenTheMiddleIsFree()
    {
        var root = CreateRoot();
        var a = AddNode(root, "a", new Rect(0, 0, 100, 100));
        var b = AddNode(root, "b", new Rect(300, 0, 100, 100));
        var line = AddLine(a, b, "orders");

        LabelPlacement.Place(root);

        Assert.Equal(Line.DefaultLabelPosition, line.LabelPosition);
        Assert.False(line.IsLabelMoved);
    }

    [Fact]
    public void Place_ShouldMoveTheLabelOffANode_ThatLiesOnTheMiddleOfTheLine()
    {
        var root = CreateRoot();
        var a = AddNode(root, "a", new Rect(0, 0, 100, 100));
        var b = AddNode(root, "b", new Rect(300, 0, 100, 100));
        var middle = AddNode(root, "middle", new Rect(185, 35, 30, 30));
        var line = AddLine(a, b, "orders");

        LabelPlacement.Place(root);

        Assert.NotEqual(Line.DefaultLabelPosition, line.LabelPosition);
        Assert.False(Overlaps(LabelBox(line), middle.Boundary));
        Assert.False(line.IsLabelMoved); // Placed, not the user's place: it is not saved
    }

    [Fact]
    public void Place_ShouldKeepTwoLabelsApart_WhenTheirLinesShareTheirMiddle()
    {
        var root = CreateRoot();
        var a = AddNode(root, "a", new Rect(0, 0, 100, 100));
        var b = AddNode(root, "b", new Rect(300, 0, 100, 100));
        var there = AddLine(a, b, "orders");
        var back = AddLine(b, a, "payments");

        LabelPlacement.Place(root);

        Assert.False(Overlaps(LabelBox(there), LabelBox(back)));
        Assert.False(Overlaps(LabelBox(back), a.Boundary));
        Assert.False(Overlaps(LabelBox(back), b.Boundary));
    }

    [Fact]
    public void Place_ShouldLeaveADraggedLabel_AndMakeRoomForIt()
    {
        var root = CreateRoot();
        var a = AddNode(root, "a", new Rect(0, 0, 100, 100));
        var b = AddNode(root, "b", new Rect(300, 0, 100, 100));
        var dragged = AddLine(b, a, "payments");
        var placed = AddLine(a, b, "orders");
        dragged.SetLabelPosition(0.5); // the user's own place, right in the middle

        LabelPlacement.Place(root);

        Assert.Equal(0.5, dragged.LabelPosition);
        Assert.True(dragged.IsLabelMoved);
        Assert.False(Overlaps(LabelBox(dragged), LabelBox(placed)));
    }

    [Fact]
    public void Place_ShouldGiveTheSamePlaces_EachTime()
    {
        var root = CreateRoot();
        var a = AddNode(root, "a", new Rect(0, 0, 100, 100));
        var b = AddNode(root, "b", new Rect(300, 0, 100, 100));
        AddNode(root, "middle", new Rect(185, 35, 30, 30));
        var there = AddLine(a, b, "orders");
        var back = AddLine(b, a, "payments");

        LabelPlacement.Place(root);
        var first = (there.LabelPosition, back.LabelPosition);
        LabelPlacement.Place(root);

        Assert.Equal(first, (there.LabelPosition, back.LabelPosition));
    }

    [Fact]
    public void SetLabelPosition_ShouldGiveTheLabelBackToThePlacement_WhenNull()
    {
        var root = CreateRoot();
        var a = AddNode(root, "a", new Rect(0, 0, 100, 100));
        var b = AddNode(root, "b", new Rect(300, 0, 100, 100));
        var line = AddLine(a, b, "orders");
        line.AutoLabelPosition = 0.34;

        line.SetLabelPosition(0.8);
        Assert.Equal(0.8, line.LabelPosition);

        line.SetLabelPosition(null);
        Assert.False(line.IsLabelMoved);
        Assert.Equal(0.34, line.LabelPosition);
    }
}
