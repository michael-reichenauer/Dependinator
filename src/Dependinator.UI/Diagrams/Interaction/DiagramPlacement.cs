using Dependinator.UI.Modeling.Models;
using Dependinator.UI.Shared.Types;

namespace Dependinator.UI.Diagrams.Interaction;

// Shared placement logic for adding manual nodes and notes at a clicked canvas position
// (used by ManualEditService and NoteService, which mirror each other's add flows).
static class DiagramPlacement
{
    // The container a new item is added into: the clicked container node when it is shown as an
    // expanded box, else the clicked node's parent, else the root.
    public static Node ResolveContainer(IModel model, PointerId pointerId)
    {
        if (pointerId.IsNode && model.Nodes.TryGetValue(pointerId.NodeId, out var node) && !node.IsRoot)
        {
            if (NodeViewPolicy.IsContainerView(node, model.Zoom))
                return node;
            if (node.Parent is { } parent)
                return parent;
        }
        return model.Root;
    }

    // Maps a pointer event's screen position to canvas (root) coordinates, then into the
    // container's inner child space, where the new item's boundary is expressed.
    public static Pos ToContainerLocal(IModel model, Node container, PointerEvent e)
    {
        var svgX = e.OffsetX * model.Zoom + model.Offset.X;
        var svgY = e.OffsetY * model.Zoom + model.Offset.Y;

        var (parentPos, parentZoom) = container.GetPosAndZoom();
        var zoom = container.ContainerZoom * parentZoom;
        var localX = (svgX - parentPos.X - container.ContainerOffset.X * parentZoom) / zoom;
        var localY = (svgY - parentPos.Y - container.ContainerOffset.Y * parentZoom) / zoom;
        return new Pos(localX, localY);
    }

    // The identity name of a manual node or note: the parent's full name and the typed short
    // name joined by a dot (mirroring parsed node names). Top-level items (empty parent name)
    // keep just the short name.
    public static string ComposeFullName(string parentName, string shortName) =>
        string.IsNullOrEmpty(parentName) ? shortName : $"{parentName}.{shortName}";
}
