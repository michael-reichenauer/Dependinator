using Dependinator.UI.Shared.Types;

namespace Dependinator.UI.Diagrams.Tiles;

record TileKey(long X, long Y, int Z, int TileWidth, int TileHeight)
{
    private const double Margin = 0.6; // How much larger than screen needs to be included in tile
    public static TileKey Empty = new(0L, 0L, 0, 0, 0);

    public double GetTileZoom() => Math.Pow(Tile.ZoomFactor, -Z);

    public Rect GetTileRect() => new(X * TileWidth, Y * TileHeight, TileWidth, TileHeight);

    public Rect GetViewRect() => new(-TileWidth * 2, -TileHeight * 2, TileWidth * 5, TileHeight * 5);

    public Rect GetTileRectWithMargin() =>
        new(
            -TileWidth * Margin,
            -TileHeight * Margin,
            TileWidth + 2 * (TileWidth * Margin),
            TileHeight + 2 * (TileHeight * Margin)
        );

    public static TileKey From(Rect canvasRect, Double canvasZoom)
    {
        var tileWidth = (int)Math.Ceiling(canvasRect.Width);
        var tileHeight = (int)Math.Ceiling(canvasRect.Height);

        int z = -(int)Math.Floor(Math.Log(canvasZoom) / Math.Log(Tile.ZoomFactor));
        double tileZoom = Math.Pow(Tile.ZoomFactor, -z);

        long x = (long)Math.Round(canvasRect.X / tileZoom / tileWidth);
        long y = (long)Math.Round(canvasRect.Y / tileZoom / tileHeight);

        return new TileKey(x, y, z, tileWidth, tileHeight);
    }

    public override string ToString() => $"(({X},{Y},{Z}))";
}
