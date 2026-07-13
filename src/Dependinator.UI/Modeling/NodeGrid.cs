namespace Dependinator.UI.Modeling;

// The snap grid for manual editing: node/note placement, moves, resizes and line segment
// points all snap to this grid.
static class NodeGrid
{
    public const double SnapSize = 20.0;

    public static double Snap(double value) => Math.Round(value / SnapSize) * SnapSize;

    public static double SnapUp(double value) => Math.Ceiling(value / SnapSize) * SnapSize;

    public static double SnapDown(double value) => Math.Floor(value / SnapSize) * SnapSize;
}
