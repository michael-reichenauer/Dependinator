using Dependinator.UI.Diagrams.Lines;
using Dependinator.UI.Shared.Types;

namespace Dependinator.UI.Tests.Diagrams;

// Where a selected line lies in the view: the spot for its toolbar and the tags at the view's
// edge that lead to an end out of view.
public class LineOnScreenTests
{
    static readonly Rect View = new(0, 0, 100, 100);

    [Fact]
    public void GetEndTag_ShouldBeNull_ForAnEndInView()
    {
        IReadOnlyList<Pos> points = [new(10, 50), new(90, 50)];

        Assert.Null(LineOnScreen.GetEndTag(points, View, isTargetEnd: false));
        Assert.Null(LineOnScreen.GetEndTag(points, View, isTargetEnd: true));
    }

    [Fact]
    public void GetEndTag_ShouldBeWhereTheLineLeavesTheView_TowardThatEnd()
    {
        IReadOnlyList<Pos> points = [new(50, 50), new(250, 150)];

        var tag = LineOnScreen.GetEndTag(points, View, isTargetEnd: true);

        Assert.Equal(new LineOnScreen.EndTag(new Pos(100, 75), LineOnScreen.Side.Right), tag);
        Assert.Null(LineOnScreen.GetEndTag(points, View, isTargetEnd: false));
    }

    [Fact]
    public void GetEndTag_ShouldFollowTheBends_FromTheEnd()
    {
        // From the source off to the left, through the view, then off and back over the top.
        IReadOnlyList<Pos> points = [new(-50, 50), new(50, 50), new(50, -40), new(200, -40)];

        Assert.Equal(
            new LineOnScreen.EndTag(new Pos(0, 50), LineOnScreen.Side.Left),
            LineOnScreen.GetEndTag(points, View, isTargetEnd: false)
        );
        Assert.Equal(
            new LineOnScreen.EndTag(new Pos(50, 0), LineOnScreen.Side.Top),
            LineOnScreen.GetEndTag(points, View, isTargetEnd: true)
        );
    }

    [Fact]
    public void GetEndTag_ShouldBeNull_ForALineThatMissesTheView()
    {
        IReadOnlyList<Pos> points = [new(-50, -50), new(200, -10)];

        Assert.Null(LineOnScreen.GetEndTag(points, View, isTargetEnd: true));
    }

    [Fact]
    public void GetAnchor_ShouldBeTheClickedSpot_WhileItIsInView()
    {
        IReadOnlyList<Pos> points = [new(0, 50), new(400, 50)];

        Assert.Equal(new Pos(20, 50), LineOnScreen.GetAnchor(points, View, 0.05));
    }

    [Fact]
    public void GetAnchor_ShouldBeTheMiddleOfThePartInView_WhenTheClickedSpotIsNot()
    {
        IReadOnlyList<Pos> points = [new(-100, 50), new(300, 50)];

        Assert.Equal(new Pos(50, 50), LineOnScreen.GetAnchor(points, View, 0.9));
        Assert.Null(LineOnScreen.GetAnchor([new(-100, -50), new(300, -50)], View, 0.5));
    }
}
