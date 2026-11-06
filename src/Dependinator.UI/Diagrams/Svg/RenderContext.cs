using Dependinator.UI.Diagrams.Lines;
using Dependinator.UI.Modeling.Models;
using Dependinator.UI.Shared.Types;

// What a render of the diagram draws with: the view state captured once per render (ViewFlags)
// and the position in the node tree being drawn (RenderContext).
namespace Dependinator.UI.Diagrams.Svg;

readonly record struct ViewFlags(bool IsEditing, bool RevealHidden, bool DimUnrelated)
{
    public RenderSelection Selection { get; init; }

    // Emphasized lines are drawn at their link-count width (Settings › Line widths by link count).
    public bool LinkCountWidths { get; init; }

    // The selection, or the picked cycle's nodes while nothing is selected (see FindDimSelection).
    public RenderSelection DimSelection { get; init; }
    public LineFilter Filter { get; init; } = LineFilter.None;
    public RowFilter Rows { get; init; } = RowFilter.None;

    // Sibling lines that are part of a circular dependency; null while cycles are not shown.
    public IReadOnlySet<LineId>? CyclicLines { get; init; }

    // The lines of the cycle picked in the cycles panel; the other cyclic lines are dimmed.
    // Null while no cycle is picked.
    public IReadOnlySet<LineId>? SelectedCycleLines { get; init; }

    // The nodes of the cycle picked in the cycles panel, dimming like a selection of them
    // while nothing else is selected. Null while no cycle is picked.
    public IReadOnlyList<NodeId>? SelectedCycleMembers { get; init; }

    // Lines of the dependency path being shown (View › Find Path); null while no path is shown.
    public IReadOnlySet<LineId>? PathLines { get; init; }

    // Lines breaking an architecture rule; null while the rules panel is closed.
    public IReadOnlySet<LineId>? ViolatingLines { get; init; }

    // The problems in or inside each node, for its badge; null while badges are off.
    public IReadOnlyDictionary<NodeId, NodeProblems>? NodeProblems { get; init; }

    // How many children of each node the user hid, for its badge; null while badges are off.
    public IReadOnlyDictionary<NodeId, int>? HiddenChildCounts { get; init; }
}

readonly record struct RenderContext(Pos CanvasOffset, double Zoom, Rect TileBounds, Pos TilePosition, ViewFlags Flags)
{
    public RenderContext With(Pos canvasOffset, double zoom) =>
        new(canvasOffset, zoom, TileBounds, TilePosition, Flags);

    public RenderContext ForNestedContainer(Rect tileRect) =>
        new(Pos.None, Zoom, TileBounds, new Pos(tileRect.X, tileRect.Y), Flags);

    // Whether a rect in tile coordinates shares some area with the tile (touching edges do not count).
    public bool IsInTile(Rect tileRect)
    {
        if (TileBounds.X + TileBounds.Width <= tileRect.X || tileRect.X + tileRect.Width <= TileBounds.X)
            return false;
        if (TileBounds.Y + TileBounds.Height <= tileRect.Y || tileRect.Y + tileRect.Height <= TileBounds.Y)
            return false;
        return true;
    }
}
