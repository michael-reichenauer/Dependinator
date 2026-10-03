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
    public void Clamp_ShouldKeepTheFrameInsideTheBox()
    {
        var clamped = MinimapGeometry.Clamp(new Rect(-20, 100, 60, 100));

        Assert.Equal(new Rect(0, 100, 40, MinimapGeometry.Height - 100), clamped);
        Assert.Equal(0, MinimapGeometry.Clamp(new Rect(500, 500, 10, 10)).Width);
    }
}
