using Dependinator.UI.Diagrams;
using Dependinator.UI.Shared.Types;

namespace Dependinator.UI.Tests.Diagrams;

// The minimap's mapping: the model bounds fit the box with one uniform scale, centered, and a
// click in the box maps back to the canvas point it shows.
public class MinimapGeometryTests
{
    [Fact]
    public void Fit_ShouldScaleUniformly_AndCenterTheBounds()
    {
        // Wide bounds: the width decides the scale and the height is centered.
        var fit = MinimapGeometry.Fit(new Rect(100, 50, 1000, 200));

        var inner = MinimapGeometry.Width - 2 * MinimapGeometry.Padding;
        Assert.Equal(inner / 1000, fit.Scale, 6);
        var mapped = fit.ToMinimap(new Rect(100, 50, 1000, 200));
        Assert.Equal(MinimapGeometry.Padding, mapped.X, 6);
        Assert.Equal(inner, mapped.Width, 6);
        Assert.Equal(MinimapGeometry.Height / 2, mapped.Y + mapped.Height / 2, 6);
    }

    [Fact]
    public void ToCanvas_ShouldInvertToMinimap()
    {
        var fit = MinimapGeometry.Fit(new Rect(-300, 700, 400, 900));
        var canvasPoint = new Pos(-120, 1000);

        var box = fit.ToMinimap(new Rect(canvasPoint.X, canvasPoint.Y, 0, 0));
        var back = fit.ToCanvas(box.X, box.Y);

        Assert.Equal(canvasPoint.X, back.X, 6);
        Assert.Equal(canvasPoint.Y, back.Y, 6);
    }

    [Fact]
    public void Window_ShouldShowTheWholeModel_WhileTheViewIsLarge()
    {
        var model = new Rect(0, 0, 2000, 1300);
        var viewport = new Rect(0, 0, 1000, 650);

        Assert.Equal(model, MinimapGeometry.Window(null, viewport, model));
    }

    [Fact]
    public void Window_ShouldZoomInWithTheView_CenteredOnIt_AndInsideTheModel()
    {
        var model = new Rect(0, 0, 5000, 3000);
        var viewport = new Rect(1000, 600, 100, 65);

        var window = MinimapGeometry.Window(null, viewport, model);

        // The frame is a fifth of the map in the tighter direction; the window has the map's shape.
        var innerWidth = MinimapGeometry.Width - 2 * MinimapGeometry.Padding;
        var innerHeight = MinimapGeometry.Height - 2 * MinimapGeometry.Padding;
        var scale = Math.Max(
            100 * MinimapGeometry.MaxZoomRatio / innerWidth,
            65 * MinimapGeometry.MaxZoomRatio / innerHeight
        );
        Assert.Equal(innerWidth * scale, window.Width, 6);
        Assert.Equal(innerHeight * scale, window.Height, 6);
        Assert.Equal(1050, window.X + window.Width / 2, 6);
        Assert.Equal(632.5, window.Y + window.Height / 2, 6);

        // Near the model's edge the window is pushed inside instead of centered.
        var edge = MinimapGeometry.Window(null, new Rect(10, 600, 100, 65), model);
        Assert.Equal(0, edge.X);
    }

    [Fact]
    public void Window_ShouldFollowLazily_WhenOnlyTheViewPositionChanges()
    {
        var model = new Rect(0, 0, 5000, 3000);
        var first = MinimapGeometry.Window(null, new Rect(1000, 600, 100, 65), model);

        // A small move keeps the window; the frame just moves inside it.
        var small = MinimapGeometry.Window(first, new Rect(1020, 610, 100, 65), model);
        Assert.Equal(first, small);

        // A move past the inner margin drags the window along by just enough.
        var far = MinimapGeometry.Window(first, new Rect(1300, 600, 100, 65), model);
        var margin = first.Width * MinimapGeometry.FollowMargin;
        Assert.Equal(1300 + 100 + margin - first.Width, far.X, 6);
        Assert.Equal(first.Y, far.Y, 6);

        // A zoom change (new size) re-centers on the view.
        var zoomed = MinimapGeometry.Window(first, new Rect(1300, 600, 50, 32.5), model);
        Assert.Equal(1325, zoomed.X + zoomed.Width / 2, 6);
    }

    [Fact]
    public void Clamp_ShouldKeepTheFrameInsideTheBox()
    {
        var clamped = MinimapGeometry.Clamp(new Rect(-20, 100, 60, 100));

        Assert.Equal(new Rect(0, 100, 40, MinimapGeometry.Height - 100), clamped);
        Assert.Equal(0, MinimapGeometry.Clamp(new Rect(500, 500, 10, 10)).Width);
    }
}
