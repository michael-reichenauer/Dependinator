using Dependinator.UI.Shared.Types;

namespace Dependinator.UI.Diagrams.Tiles;

record Tile(TileKey Key, string Svg, double Zoom, Pos Offset)
{
    public static readonly Tile Empty = new(TileKey.Empty, "", 1.0, Pos.None);
}
