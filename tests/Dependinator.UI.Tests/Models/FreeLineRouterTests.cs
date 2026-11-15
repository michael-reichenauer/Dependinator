using Dependinator.UI.Modeling.Layout;
using Dependinator.UI.Shared.Types;

namespace Dependinator.UI.Tests.Models;

public class FreeLineRouterTests
{
    [Fact]
    public void Route_ShouldNotBend_WhenTheStraightLineIsClear()
    {
        var points = FreeLineRouter.Route(new Pos(0, 50), new Pos(400, 50), [new Rect(150, 200, 100, 100)]);

        Assert.Empty(points);
    }

    [Fact]
    public void Route_ShouldBendAroundANodeInTheWay()
    {
        Rect[] obstacles = [new Rect(150, 0, 100, 100)];
        var start = new Pos(0, 50);
        var end = new Pos(400, 50);

        var points = FreeLineRouter.Route(start, end, obstacles);

        var bend = Assert.Single(points);
        Assert.Equal(200, bend.X);
        Assert.True(bend.Y < 0 || bend.Y > 100, $"Expected the bend above or below the node, got {bend}");
        Assert.False(FreeLineRouter.IsBlocked([start, .. points, end], obstacles));
    }

    [Fact]
    public void Route_ShouldPassBetweenStackedNodes_WhenTheGapIsWideEnough()
    {
        // The line runs just below the upper node; going above it would be a longer way round.
        Rect[] obstacles = [new Rect(150, 0, 100, 100), new Rect(150, 140, 100, 100)];
        var start = new Pos(0, 90);
        var end = new Pos(400, 90);

        var points = FreeLineRouter.Route(start, end, obstacles);

        var bend = Assert.Single(points);
        Assert.Equal(new Pos(200, 120), bend);
        Assert.False(FreeLineRouter.IsBlocked([start, .. points, end], obstacles));
    }

    [Fact]
    public void Route_ShouldGoPastStackedNodes_WhenTheGapIsTooNarrow()
    {
        Rect[] obstacles = [new Rect(150, 0, 100, 100), new Rect(150, 110, 100, 100)];
        var start = new Pos(0, 90);
        var end = new Pos(400, 90);

        var points = FreeLineRouter.Route(start, end, obstacles);

        Assert.NotEmpty(points);
        Assert.DoesNotContain(points, point => point.Y > 100 && point.Y < 110);
        Assert.False(FreeLineRouter.IsBlocked([start, .. points, end], obstacles));
    }

    [Fact]
    public void Route_ShouldBendBesideANode_WhenTheLineRunsDown()
    {
        Rect[] obstacles = [new Rect(0, 150, 100, 100)];
        var start = new Pos(50, 0);
        var end = new Pos(60, 400);

        var points = FreeLineRouter.Route(start, end, obstacles);

        var bend = Assert.Single(points);
        Assert.Equal(200, bend.Y);
        Assert.True(bend.X < 0 || bend.X > 100, $"Expected the bend beside the node, got {bend}");
    }

    [Fact]
    public void Route_ShouldGoAroundSeveralNodesInARow()
    {
        Rect[] obstacles = [new Rect(100, 0, 100, 100), new Rect(300, 0, 100, 100), new Rect(500, 0, 100, 100)];
        var start = new Pos(0, 50);
        var end = new Pos(700, 50);

        var points = FreeLineRouter.Route(start, end, obstacles);

        Assert.NotEmpty(points);
        Assert.False(FreeLineRouter.IsBlocked([start, .. points, end], obstacles));
    }

    [Fact]
    public void IsBlocked_ShouldCountALinePassingWithinTheClearance()
    {
        Rect[] obstacles = [new Rect(150, 0, 100, 100)];

        Assert.True(FreeLineRouter.IsBlocked([new Pos(0, 104), new Pos(400, 104)], obstacles));
        Assert.False(FreeLineRouter.IsBlocked([new Pos(0, 120), new Pos(400, 120)], obstacles));
    }
}
