using Dependinator.UI.Shared.Types;

namespace Dependinator.UI.Modeling.Layout;

// The input of the layered layout of one container, detached from the model (no Node or Line
// references), so it can be computed without holding the model lock and tested on its own.
// Vertices are in ordinal name order, the layout's tie-break order; edges index into them.
sealed record LayoutGraph(
    IReadOnlyList<LayoutVertex> Vertices,
    IReadOnlyList<LayoutEdge> Edges,
    LayoutMetrics Metrics,
    double ParentAspect
);

// A child to arrange: its name (the tie-breaker), its size, and the weight of the lines that
// cross the container's edge into it (entering at the left) and out of it (leaving at the right).
readonly record struct LayoutVertex(string Name, double Width, double Height, double ExternalIn, double ExternalOut);

// A line between two children, weighted by its link count. A hidden line still shapes the layout
// but gets no waypoints; a line the user routed keeps the user's waypoints.
readonly record struct LayoutEdge(int Source, int Target, double Weight, bool IsHidden, bool IsUserRouted);

// A computed layout: a boundary per vertex and the waypoints per edge (null: leave the line's
// waypoints as they are; empty: none).
sealed record LayoutResult(IReadOnlyList<Rect> Boundaries, IReadOnlyList<IReadOnlyList<Pos>?> Waypoints);
