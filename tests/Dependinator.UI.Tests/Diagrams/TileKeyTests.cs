using Dependinator.UI.Diagrams.Tiles;
using Dependinator.UI.Shared.Types;

namespace Dependinator.UI.Tests.Diagrams;

public class TileKeyTests
{
    [Theory]
    [InlineData(1.0, 0)] // Level 0 is unzoomed
    [InlineData(1.05, 0)] // Within the level 0 zoom band
    [InlineData(1.2, -1)] // Zoomed in one level
    [InlineData(0.9, 2)] // Zoomed out two levels
    [InlineData(0.5, 8)]
    public void From_ShouldMapZoomToLevels(double canvasZoom, int expectedZ)
    {
        TileKey key = TileKey.From(new Rect(0, 0, 1000, 500), canvasZoom);

        Assert.Equal(expectedZ, key.Z);
    }

    [Fact]
    public void From_ShouldComputeTileIndices()
    {
        TileKey key = TileKey.From(new Rect(2000, -1000, 1000, 500), 1.0);

        Assert.Equal(2, key.X);
        Assert.Equal(-2, key.Y);
        Assert.Equal(1000, key.TileWidth);
        Assert.Equal(500, key.TileHeight);
    }

    [Theory]
    [InlineData(0, 1.0)]
    [InlineData(3, 1.0 / (1.1 * 1.1 * 1.1))] // ZoomFactor^-3
    [InlineData(-2, 1.1 * 1.1)] // ZoomFactor^2
    public void GetTileZoom_ShouldInvertLevel(int z, double expectedZoom)
    {
        TileKey key = new TileKey(0, 0, z, 1000, 500);

        Assert.Equal(expectedZoom, key.GetTileZoom(), 12);
    }

    [Fact]
    public void GetTileRect_ShouldScaleIndicesByTileSize()
    {
        TileKey key = new TileKey(2, -2, 0, 1000, 500);

        Assert.Equal(new Rect(2000, -1000, 1000, 500), key.GetTileRect());
    }
}
