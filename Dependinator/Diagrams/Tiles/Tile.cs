using Dependinator.Models;

namespace Dependinator.Diagrams.Tiles;

record Tile(TileKey Key, string Svg, double Zoom, Pos Offset)
{
    public const double ZoomFactor = 1.1; // How often is a new tile needed when zooming
    public static Tile Empty = new(TileKey.Empty, "", 1.0, Pos.None);
}
