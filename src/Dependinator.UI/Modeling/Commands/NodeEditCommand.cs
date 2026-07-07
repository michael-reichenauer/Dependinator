using Dependinator.UI.Modeling.Models;
using Dependinator.UI.Shared.Types;

namespace Dependinator.UI.Modeling.Commands;

class NodeEditCommand(NodeId nodeId) : Command
{
    readonly NodeId nodeId = nodeId;

    public Rect? Boundary { get; set; }
    public Rect? BoundaryCopy { get; set; }
    public Double? ContainerZoom { get; set; }
    public Double? ContainerZoomCopy { get; set; }
    public Pos? ContainerOffset { get; set; }
    public Pos? ContainerOffsetCopy { get; set; }

    // Custom icon name; null means "not part of this command", "" means clear to the
    // node-type default icon (stored as null on the node).
    public string? IconName { get; set; }
    public string? IconNameCopy { get; set; }

    // Description; null means "not part of this command", "" means clear to no description
    // (stored as null on the node). Used for editing note text.
    public string? Description { get; set; }
    public string? DescriptionCopy { get; set; }

    public override void Execute(IModel model)
    {
        if (!model.Nodes.TryGetValue(nodeId, out var node))
            return;

        if (Boundary != null)
            (BoundaryCopy, node.Boundary) = (node.Boundary, Boundary);
        if (ContainerZoom != null)
            (ContainerZoomCopy, node.ContainerZoom) = (node.ContainerZoom, (double)ContainerZoom);
        if (ContainerOffset != null)
            (ContainerOffsetCopy, node.ContainerOffset) = (node.ContainerOffset, ContainerOffset);
        if (IconName != null)
            (IconNameCopy, node.CustomIconName) = (node.CustomIconName ?? "", IconName == "" ? null : IconName);
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
                (double)ContainerZoomCopy,
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
        if (DescriptionCopy != null)
        {
            Description = node.Description ?? "";
            node.SetDescription(DescriptionCopy == "" ? null : DescriptionCopy);
            DescriptionCopy = null;
        }
    }
}
