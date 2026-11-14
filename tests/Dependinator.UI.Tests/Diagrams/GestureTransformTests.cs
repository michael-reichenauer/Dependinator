using Dependinator.UI.Diagrams;
using Dependinator.UI.Shared.Types;

namespace Dependinator.UI.Tests.Diagrams;

// The transform that moves the shown svg with a pan or zoom (GestureTransform): it must put
// every canvas point where the current view would draw it, and give up where the shown tile
// lacks content or the scale gets too large.
public class GestureTransformTests
{
    static readonly Rect Svg = new(0, 0, 1000, 800);

    // The tile around a view at (0,0), zoom 1, with plenty of margin.
    static readonly Rect TileArea = new(-600, -480, 2200, 1760);

    static Pos ShownPixel(Pos canvas, Pos shownOffset, double shownZoom) =>
        new((canvas.X - shownOffset.X) / shownZoom, (canvas.Y - shownOffset.Y) / shownZoom);

    [Theory]
    [InlineData(120, -40, 1)] // a pan
    [InlineData(50, 30, 0.8)] // zoomed in
    [InlineData(-90, -60, 1.25)] // zoomed out
    [InlineData(-200, -150, 1.6)] // zoomed out further, still inside the tile
    public void Get_ShouldPutCanvasPointsWhereTheCurrentViewDrawsThem(double x, double y, double zoom)
    {
        var shownOffset = new Pos(0, 0);
        var offset = new Pos(x, y);

        var t = Assert.NotNull(GestureTransform.Get(shownOffset, 1, offset, zoom, Svg, TileArea));

        foreach (var canvas in new[] { new Pos(0, 0), new Pos(300, 200), new Pos(-50, 700) })
        {
            var q = ShownPixel(canvas, shownOffset, 1);
            var screen = new Pos(q.X * t.Scale + t.X, q.Y * t.Scale + t.Y);
            Assert.Equal((canvas.X - offset.X) / zoom, screen.X, 6);
            Assert.Equal((canvas.Y - offset.Y) / zoom, screen.Y, 6);
        }
    }

    [Fact]
    public void Get_ShouldBeIdentity_WhenTheViewHasNotMoved()
    {
        var t = GestureTransform.Get(new Pos(10, 20), 2, new Pos(10, 20), 2, Svg, new Rect(-1000, -1000, 5000, 5000));

        Assert.Equal((0.0, 0.0, 1.0), t);
    }

    [Theory]
    [InlineData(0.45)] // zoomed in too far: the painted layer would blur
    [InlineData(2.2)] // zoomed out too far
    public void Get_ShouldBeNull_WhenTheScaleIsTooLarge(double zoom)
    {
        Assert.Null(GestureTransform.Get(new Pos(0, 0), 1, new Pos(0, 0), zoom, Svg, new Rect(-5000, -5000, 1e4, 1e4)));
    }

    [Theory]
    [InlineData(-560, 0)] // past the tile's left edge
    [InlineData(700, 0)] // past its right edge
    [InlineData(0, 600)] // past its bottom
    public void Get_ShouldBeNull_WhenTheViewLeavesTheTile(double x, double y)
    {
        Assert.Null(GestureTransform.Get(new Pos(0, 0), 1, new Pos(x, y), 1, Svg, TileArea));
    }

    [Fact]
    public void Get_ShouldBeNull_WithoutAShownTile()
    {
        Assert.Null(GestureTransform.Get(new Pos(0, 0), 1, new Pos(1, 1), 1, Svg, Rect.None));
        Assert.Null(GestureTransform.Get(new Pos(0, 0), 0, new Pos(1, 1), 1, Svg, TileArea));
    }

    [Fact]
    public void Css_ShouldBeEmpty_WithoutATransform()
    {
        Assert.Equal("", GestureTransform.Css(null));
        Assert.Contains("translate(1.5px,-2px) scale(1.2)", GestureTransform.Css((1.5, -2, 1.2)));
    }
}
