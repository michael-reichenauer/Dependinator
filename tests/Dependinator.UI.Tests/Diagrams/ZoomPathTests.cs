using Dependinator.UI.Diagrams.Interaction;
using Dependinator.UI.Shared.Types;

namespace Dependinator.UI.Tests.Diagrams;

// The go to flight: one path that pans and zooms at once (van Wijk and Nuij).
public class ZoomPathTests
{
    [Fact]
    public void AtTime_ShouldStartAndEndAtTheGivenViews()
    {
        var path = new ZoomPath(new Pos(0, 0), 10, new Pos(1000, 500), 2);

        var (start, startWidth) = path.AtTime(0, dwell: 1.5);
        var (end, endWidth) = path.AtTime(1, dwell: 1.5);

        Assert.Equal(0, start.X, 6);
        Assert.Equal(0, start.Y, 6);
        Assert.Equal(10, startWidth, 6);
        Assert.Equal(1000, end.X, 6);
        Assert.Equal(500, end.Y, 6);
        Assert.Equal(2, endWidth, 6);
    }

    [Fact]
    public void AtTime_ShouldZoomOutAboutAsFarAsTheJump_ForALongJump()
    {
        var path = new ZoomPath(new Pos(0, 0), 10, new Pos(1000, 0), 10);

        var maxWidth = Enumerable.Range(0, 101).Max(i => path.AtTime(i / 100.0, dwell: 0).Width);

        // At the top of the flight both ends are in the view's focus area.
        Assert.InRange(maxWidth, 950, 1100);
    }

    [Fact]
    public void AtTime_ShouldMoveSteadilyTowardTheTarget()
    {
        var path = new ZoomPath(new Pos(0, 0), 10, new Pos(1000, 300), 1);

        var xs = Enumerable.Range(0, 51).Select(i => path.AtTime(i / 50.0, dwell: 1.5).Center.X).ToList();

        Assert.True(xs.Zip(xs.Skip(1)).All(p => p.Second >= p.First - 1e-9));
    }

    [Fact]
    public void AtTime_ShouldOnlyZoom_WhenTheCentersAreTheSame()
    {
        var path = new ZoomPath(new Pos(5, 5), 100, new Pos(5, 5), 1);

        var (middle, middleWidth) = path.AtTime(0.5, dwell: 1.5);

        Assert.Equal(5, middle.X, 6);
        Assert.Equal(5, middle.Y, 6);
        Assert.Equal(10, middleWidth, 6); // Halfway in log scale
        Assert.True(path.Length > 0);
    }

    [Fact]
    public void AtTime_ShouldLingerAtTheTop_WithDwell()
    {
        var path = new ZoomPath(new Pos(0, 0), 10, new Pos(1000, 0), 10);

        // The share of the time spent with the view at least half as wide as at the top.
        double TopShare(double dwell)
        {
            var widths = Enumerable.Range(0, 201).Select(i => path.AtTime(i / 200.0, dwell).Width).ToList();
            var max = widths.Max();
            return widths.Count(w => w >= max / 2) / 201.0;
        }

        Assert.True(TopShare(1.5) > TopShare(0) * 1.3);
    }
}
