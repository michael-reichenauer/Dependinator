using Dependinator.UI.Shared.Types;

namespace Dependinator.UI.Diagrams.Tiles;

// Identifies a cached tile. Z is a discrete zoom level, where each level covers a ZoomFactor
// zoom band and tile content is rendered at the level zoom ZoomFactor^-Z. X and Y are tile
// indices at that level, in units of the screen size (TileWidth/TileHeight).
record TileKey(long X, long Y, int Z, int TileWidth, int TileHeight)
{
    // How often a new tile is needed when zooming: each level covers a ZoomFactor band, and
    // in-between zooms scale the tile via the outer svg viewBox (lossless for vector content).
    // Must stay above PanZoomService.WheelZoomSpeed so consecutive wheel ticks can reuse the
    // tile instead of re-rendering on every tick.
    public const double ZoomFactor = 1.3;

    // Rendered tile content extends Margin tile sizes beyond the tile on each side; the tile
    // svg viewBox extends ViewBoxMargin tile sizes on each side (must be >= Margin), so the
    // rendered content always fits within the viewBox.
    const double Margin = 0.6; // How much larger than screen needs to be included in tile
    const int ViewBoxMargin = 2;

    public static readonly TileKey Empty = new(0L, 0L, 0, 0, 0);

    public double GetTileZoom() => LevelZoom(Z);

    // The tile rect in level canvas coordinates (canvas coordinates scaled by the level zoom)
    public Rect GetTileRect() => new(X * TileWidth, Y * TileHeight, TileWidth, TileHeight);

    // The svg viewBox in tile local coordinates (origin at the tile top-left corner)
    public Rect GetViewRect() =>
        new(
            -TileWidth * ViewBoxMargin,
            -TileHeight * ViewBoxMargin,
            TileWidth * (2 * ViewBoxMargin + 1),
            TileHeight * (2 * ViewBoxMargin + 1)
        );

    // The area to render in tile local coordinates (origin at the tile top-left corner)
    public Rect GetTileRectWithMargin() =>
        new(
            -TileWidth * Margin,
            -TileHeight * Margin,
            TileWidth + 2 * (TileWidth * Margin),
            TileHeight + 2 * (TileHeight * Margin)
        );

    public static TileKey From(Rect canvasRect, double canvasZoom)
    {
        var tileWidth = (int)Math.Ceiling(canvasRect.Width);
        var tileHeight = (int)Math.Ceiling(canvasRect.Height);

        int z = -(int)Math.Floor(Math.Log(canvasZoom) / Math.Log(ZoomFactor));
        double tileZoom = LevelZoom(z);

        long x = (long)Math.Round(canvasRect.X / tileZoom / tileWidth);
        long y = (long)Math.Round(canvasRect.Y / tileZoom / tileHeight);

        return new TileKey(x, y, z, tileWidth, tileHeight);
    }

    static double LevelZoom(int z) => Math.Pow(ZoomFactor, -z);

    public override string ToString() => $"(({X},{Y},{Z}))";
}
