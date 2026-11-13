using Dependinator.Core.Parsing;
using Dependinator.UI.Diagrams.Editing;
using Dependinator.UI.Diagrams.Interaction;
using Dependinator.UI.Modeling;
using Dependinator.UI.Modeling.Commands;
using Dependinator.UI.Modeling.Models;
using Dependinator.UI.Shared;
using Dependinator.UI.Shared.Types;
using Node = Dependinator.UI.Modeling.Models.Node;

namespace Dependinator.UI.Tests.Diagrams;

// Bends are made on the line itself: dragging a segment handle adds a bend point, one dragged
// back straight or double-clicked goes away, and Straighten removes them all.
public class LineEditServiceTests
{
    // A line from a's right edge (100, 50) to b's left edge (300, 50), at zoom 1.
    static (LineEditService Service, CommandService Commands, Line Line) Create()
    {
        var modelMgr = new ModelMgr(new StateMgr());
        Line line;
        using (var model = modelMgr.UseModel())
        {
            model.Root.ContainerZoom = 1;
            var a = new Node("A", model.Root) { Boundary = new Rect(0, 0, 100, 100), Type = NodeType.ClassType };
            var b = new Node("B", model.Root) { Boundary = new Rect(300, 0, 100, 100), Type = NodeType.ClassType };
            model.Root.AddChild(a);
            model.Root.AddChild(b);
            model.TryAddNode(a);
            model.TryAddNode(b);
            line = new Line(a, b);
            model.TryAddLine(line);
        }
        var commands = new CommandService(Mock.Of<IApplicationEvents>(), modelMgr);
        return (new LineEditService(modelMgr, commands), commands, line);
    }

    [Fact]
    public void InsertSegmentPoint_ShouldAddABend_AtTheHandleMovedByTheDrag()
    {
        var (service, _, line) = Create();

        var pointId = service.InsertSegmentPoint(PointerId.FromLineMidPoint(line.Id, 0), 0, 40, 1);

        Assert.Equal(PointerId.FromLinePoint(line.Id, 0), pointId);
        Assert.Equal([new Pos(200, 90)], line.SegmentPoints);
        Assert.True(line.IsSegmentsUserSet);
    }

    [Fact]
    public void InsertSegmentPoint_ShouldKeepThePointsInOrder_OnALaterSegment()
    {
        var (service, _, line) = Create();
        line.SetSegmentPoints([new Pos(200, 150)]);

        // The second segment runs from the bend (200, 150) to b's edge (300, 50); a quarter along.
        var pointId = service.InsertSegmentPoint(PointerId.FromLineMidPoint(line.Id, 1, 25), 0, 0, 1);

        Assert.Equal(PointerId.FromLinePoint(line.Id, 1), pointId);
        Assert.Equal([new Pos(200, 150), new Pos(225, 125)], line.SegmentPoints);
    }

    [Fact]
    public void InsertSegmentPoint_ShouldDoNothing_ForAMissingSegment()
    {
        var (service, _, line) = Create();

        Assert.Null(service.InsertSegmentPoint(PointerId.FromLineMidPoint(line.Id, 1), 0, 40, 1));
        Assert.Empty(line.SegmentPoints);
    }

    [Fact]
    public void EndSegmentPointMove_ShouldRemoveABend_DraggedBackStraight()
    {
        var (service, _, line) = Create();
        line.SetSegmentPoints([new Pos(203, 54)]);

        service.EndSegmentPointMove(PointerId.FromLinePoint(line.Id, 0), 1);

        Assert.Empty(line.SegmentPoints);
    }

    [Fact]
    public void EndSegmentPointMove_ShouldSnapABend_ThatBendsTheLine()
    {
        var (service, _, line) = Create();
        line.SetSegmentPoints([new Pos(203, 93)]);

        service.EndSegmentPointMove(PointerId.FromLinePoint(line.Id, 0), 1);

        Assert.Equal([new Pos(200, 100)], line.SegmentPoints);
    }

    [Fact]
    public void EndSegmentPointMove_ShouldUseScreenPixels_ForWhatCountsAsStraight()
    {
        var (service, _, line) = Create();
        line.SetSegmentPoints([new Pos(200, 60)]);

        // 10 units off the line are 5 screen pixels at zoom 2 (zoomed out), so straight.
        service.EndSegmentPointMove(PointerId.FromLinePoint(line.Id, 0), 2);

        Assert.Empty(line.SegmentPoints);
    }

    [Fact]
    public async Task InsertThenEnd_ShouldBeOneUndoStep()
    {
        var (service, commands, line) = Create();

        var pointId = service.InsertSegmentPoint(PointerId.FromLineMidPoint(line.Id, 0), 3, 43, 1);
        service.EndSegmentPointMove(pointId!, 1);
        Assert.Equal([new Pos(200, 100)], line.SegmentPoints);

        await commands.Undo();
        Assert.Empty(line.SegmentPoints);
    }

    [Fact]
    public void RemoveSegmentPoint_ShouldRemoveThatBend()
    {
        var (service, _, line) = Create();
        line.SetSegmentPoints([new Pos(150, 100), new Pos(250, 100)]);

        service.RemoveSegmentPoint(PointerId.FromLinePoint(line.Id, 0));

        Assert.Equal([new Pos(250, 100)], line.SegmentPoints);
    }

    [Fact]
    public async Task StraightenLine_ShouldRemoveAllBends_AndUndoBringsThemBack()
    {
        var (service, commands, line) = Create();
        line.SetSegmentPoints([new Pos(150, 100), new Pos(250, 100)]);
        line.IsSegmentsUserSet = true;

        service.StraightenLine(line.Id);
        Assert.Empty(line.SegmentPoints);
        Assert.False(line.IsSegmentsUserSet);

        await commands.Undo();
        Assert.Equal([new Pos(150, 100), new Pos(250, 100)], line.SegmentPoints);
        Assert.True(line.IsSegmentsUserSet);
    }
}
