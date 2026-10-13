using Dependinator.UI.Diagrams;
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
}
