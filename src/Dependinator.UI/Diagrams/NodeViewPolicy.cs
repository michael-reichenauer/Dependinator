using Dependinator.UI.Modeling.Models;

namespace Dependinator.UI.Diagrams;

// How a node is presented at a given zoom level: as an icon, as an expanded container with
// visible children, or not at all because the view has zoomed in past it.
static class NodeViewPolicy
{
    const double MaxNodeZoom = 8 * 1 / Node.DefaultContainerZoom; // To large to be seen
    const double MinContainerZoom = 2.0;

    public static bool IsToLargeToBeSeen(double zoom) => zoom > MaxNodeZoom;

    public static bool IsShowIcon(Parsing.NodeType nodeType, double zoom) =>
        nodeType.IsMember || zoom <= MinContainerZoom;

    // The node is shown as an expanded box with visible children (not an icon and not zoomed
    // past visibility).
    public static bool IsContainerView(Node node, double modelZoom)
    {
        var nodeZoom = 1 / (node.GetZoom() * modelZoom);
        return !IsToLargeToBeSeen(nodeZoom) && !IsShowIcon(node.Type, nodeZoom);
    }
}
