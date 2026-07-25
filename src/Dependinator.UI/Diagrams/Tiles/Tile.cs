using Dependinator.UI.Shared.Types;

namespace Dependinator.UI.Diagrams.Tiles;

record Tile(TileKey Key, string Svg, double Zoom, Pos Offset)
{
    public static readonly Tile Empty = new(TileKey.Empty, "", 1.0, Pos.None);

    // Strings are UTF-16, so the svg dominates the tile's memory; the key, zoom, offset and
    // object headers are a rounding error next to a svg of tens of thousands of chars.
    public long ByteSize => (long)Svg.Length * 2;
}
