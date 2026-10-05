using Dependinator.UI.Modeling.Models;
using Dependinator.UI.Shared.Types;

namespace Dependinator.UI.Diagrams.Interaction;

// Which nodes a rubber band picks: every node whose box lies fully inside the band, at the
// level the user is looking at. A container that only partly overlaps the band is looked
// into when its children are drawn (open), so a band dragged inside an open container picks
// the children inside it; a closed container (an icon) is taken or left as a whole, and a node
// inside a selected container is never added on its own. Notes are left out: they are not part
// of a group (one would otherwise become the group's primary node, with the note toolbar).
static class RubberBandSelection
{
    public static IReadOnlyList<Node> FindNodes(
        Node root,
        Rect band,
        Func<Node, bool> isChildrenShown,
        Func<Node, bool> isVisible
    )
    {
        var found = new List<Node>();
        Visit(root);
        return found;

        void Visit(Node parent)
        {
            foreach (var child in parent.Children)
            {
                if (child.IsNote || !isVisible(child))
                    continue;
                var rect = CanvasRect(child);
                if (IsInside(rect, band))
                {
                    found.Add(child);
                    continue;
                }
                if (Overlaps(rect, band) && isChildrenShown(child))
                    Visit(child);
            }
        }
    }

    static Rect CanvasRect(Node node)
    {
        var (pos, zoom) = node.GetPosAndZoom();
        return new Rect(pos.X, pos.Y, node.Boundary.Width * zoom, node.Boundary.Height * zoom);
    }

    static bool IsInside(Rect inner, Rect outer) =>
        inner.X >= outer.X
        && inner.Y >= outer.Y
        && inner.X + inner.Width <= outer.X + outer.Width
        && inner.Y + inner.Height <= outer.Y + outer.Height;

    static bool Overlaps(Rect a, Rect b) =>
        a.X < b.X + b.Width && b.X < a.X + a.Width && a.Y < b.Y + b.Height && b.Y < a.Y + a.Height;
}
