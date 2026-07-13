using Dependinator.UI.Modeling.Models;
using Dependinator.UI.Shared.Types;

namespace Dependinator.UI.Modeling.Commands;

// Edits node properties (boundary, container zoom/offset, icon, description). A null property
// means "not part of this command". Execute stores the node's previous value in the matching
// *Copy property; Revert swaps it back, so Execute/Revert can be replayed for redo/undo.
class NodeEditCommand(NodeId nodeId) : Command
{
    readonly NodeId nodeId = nodeId;

    // Only edits to the same node merge into one undo step (e.g. a drag gesture).
    public override string MergeKey => nodeId.Value;

    public Rect? Boundary { get; set; }
    public Rect? BoundaryCopy { get; private set; }
    public double? ContainerZoom { get; set; }
    public double? ContainerZoomCopy { get; private set; }
    public Pos? ContainerOffset { get; set; }
    public Pos? ContainerOffsetCopy { get; private set; }

    // Custom icon name; "" means clear to the node-type default icon (stored as null on the node).
    public string? IconName { get; set; }
    public string? IconNameCopy { get; private set; }

    // Custom icon tint; "" means clear to the default violet (stored as null on the node).
    public string? IconColor { get; set; }
    public string? IconColorCopy { get; private set; }

    // Custom container color; "" means clear to the auto-assigned color (stored as null on the node).
    public string? CustomColor { get; set; }
    public string? CustomColorCopy { get; private set; }

    // Description; "" means clear to no description (stored as null on the node). Used for
    // editing note text.
    public string? Description { get; set; }
    public string? DescriptionCopy { get; private set; }

    public override void Execute(IModel model)
    {
        if (!model.Nodes.TryGetValue(nodeId, out var node))
            return;

        if (Boundary != null)
            (BoundaryCopy, node.Boundary) = (node.Boundary, Boundary);
        if (ContainerZoom != null)
            (ContainerZoomCopy, node.ContainerZoom) = (node.ContainerZoom, ContainerZoom.Value);
        if (ContainerOffset != null)
            (ContainerOffsetCopy, node.ContainerOffset) = (node.ContainerOffset, ContainerOffset);
        if (IconName != null)
            (IconNameCopy, node.CustomIconName) = (node.CustomIconName ?? "", IconName == "" ? null : IconName);
        if (IconColor != null)
            (IconColorCopy, node.CustomIconColor) = (node.CustomIconColor ?? "", IconColor == "" ? null : IconColor);
        if (CustomColor != null)
            (CustomColorCopy, node.CustomColor) = (node.CustomColor ?? "", CustomColor == "" ? null : CustomColor);
        if (Description != null)
        {
            DescriptionCopy = node.Description ?? "";
            node.SetDescription(Description == "" ? null : Description);
        }
    }

    public override void Revert(IModel model)
    {
        if (!model.Nodes.TryGetValue(nodeId, out var node))
            return;

        if (BoundaryCopy != null)
            (Boundary, node.Boundary, BoundaryCopy) = (node.Boundary, BoundaryCopy, null);
        if (ContainerZoomCopy != null)
            (ContainerZoom, node.ContainerZoom, ContainerZoomCopy) = (
                node.ContainerZoom,
                ContainerZoomCopy.Value,
                null
            );
        if (ContainerOffsetCopy != null)
            (ContainerOffset, node.ContainerOffset, ContainerOffsetCopy) = (
                node.ContainerOffset,
                ContainerOffsetCopy,
                null
            );
        if (IconNameCopy != null)
            (IconName, node.CustomIconName, IconNameCopy) = (
                node.CustomIconName ?? "",
                IconNameCopy == "" ? null : IconNameCopy,
                null
            );
        if (IconColorCopy != null)
            (IconColor, node.CustomIconColor, IconColorCopy) = (
                node.CustomIconColor ?? "",
                IconColorCopy == "" ? null : IconColorCopy,
                null
            );
        if (CustomColorCopy != null)
            (CustomColor, node.CustomColor, CustomColorCopy) = (
                node.CustomColor ?? "",
                CustomColorCopy == "" ? null : CustomColorCopy,
                null
            );
        if (DescriptionCopy != null)
        {
            Description = node.Description ?? "";
            node.SetDescription(DescriptionCopy == "" ? null : DescriptionCopy);
            DescriptionCopy = null;
        }
    }
}
