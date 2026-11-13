using Dependinator.UI.Diagrams;
using Dependinator.UI.Diagrams.Lines;
using Dependinator.UI.Shared.Types;

namespace Dependinator.UI.Tests.Diagrams;

// A line's label sits halfway along the polyline's length, not at the middle vertex.
public class LinePathGeometryMidPointTests
{
    [Fact]
    public void MidPoint_ShouldBeTheMiddle_OfAStraightLine()
    {
        var middle = LinePathGeometry.MidPoint([new Pos(0, 0), new Pos(100, 50)]);

        Assert.Equal(50, middle.X, 6);
        Assert.Equal(25, middle.Y, 6);
    }

    [Fact]
    public void MidPoint_ShouldFollowTheLength_OfABentLine()
    {
        // 100 right, then 20 down: half of 120 is 60 along the first segment.
        var middle = LinePathGeometry.MidPoint([new Pos(0, 0), new Pos(100, 0), new Pos(100, 20)]);

        Assert.Equal(60, middle.X, 6);
        Assert.Equal(0, middle.Y, 6);
    }

    [Fact]
    public void MidPoint_ShouldHandleDegenerateInput()
    {
        Assert.Equal(new Pos(5, 5), LinePathGeometry.MidPoint([new Pos(5, 5)]));
        Assert.Equal(Pos.None, LinePathGeometry.MidPoint([]));
        Assert.Equal(new Pos(5, 5), LinePathGeometry.MidPoint([new Pos(5, 5), new Pos(5, 5)]));
    }

    [Fact]
    public void PointAt_ShouldFollowTheLength_AndFractionAtShouldInvertIt()
    {
        // 100 right, then 20 down (120 long): a quarter is 30 along the first segment, 0.9 is
        // 8 down the second.
        IReadOnlyList<Pos> points = [new Pos(0, 0), new Pos(100, 0), new Pos(100, 20)];

        Assert.Equal(new Pos(30, 0), LinePathGeometry.PointAt(points, 0.25));
        var late = LinePathGeometry.PointAt(points, 0.9);
        Assert.Equal(100, late.X, 6);
        Assert.Equal(8, late.Y, 6);

        Assert.Equal(0.25, LinePathGeometry.FractionAt(points, new Pos(30, 0)), 6);
        Assert.Equal(0.9, LinePathGeometry.FractionAt(points, new Pos(100, 8)), 6);
    }

    [Fact]
    public void FractionAt_ShouldTakeTheClosestPointOnTheLine_ForAPointBesideIt()
    {
        IReadOnlyList<Pos> points = [new Pos(0, 0), new Pos(100, 0)];

        Assert.Equal(0.4, LinePathGeometry.FractionAt(points, new Pos(40, 25)), 6);
        Assert.Equal(0, LinePathGeometry.FractionAt(points, new Pos(-50, 10)), 6);
        Assert.Equal(1, LinePathGeometry.FractionAt(points, new Pos(500, -10)), 6);
        Assert.Equal(0.5, LinePathGeometry.FractionAt([new Pos(5, 5)], new Pos(0, 0)), 6);
    }
}
