using Dependinator.UI.Modeling.Models;
using Dependinator.UI.Shared.Types;

namespace Dependinator.UI.Diagrams.Interaction;

// Which nodes a zone "contains": the siblings whose boundary lies fully inside the zone's (a node
// that only overlaps the edge is not in the group). Dragging a zone moves them with it; the
// structure is untouched, containment is purely where the user drew the zone.
static class ZoneContents
{
    public static IReadOnlyList<Node> NodesInside(Node zone) =>
        zone.Parent is not { } parent
            ? []
            : parent.Children.Where(child => child != zone && IsInside(child.Boundary, zone.Boundary)).ToList();

    static bool IsInside(Rect inner, Rect outer) =>
        inner.X >= outer.X
        && inner.Y >= outer.Y
        && inner.X + inner.Width <= outer.X + outer.Width
        && inner.Y + inner.Height <= outer.Y + outer.Height;
}
