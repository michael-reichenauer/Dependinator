

namespace Dependinator.Models;


class NodeEditCommand : ICommand
{
    readonly NodeId nodeId;

    public Rect? Bounds { get; set; }
    public Rect? BoundsCopy { get; set; }
    public Double? ContainerZoom { get; set; }
    public Double? ContainerZoomCopy { get; set; }
    public Pos? ContainerOffset { get; set; }
    public Pos? ContainerOffsetCopy { get; set; }

    public NodeEditCommand(NodeId nodeId)
    {
        this.nodeId = nodeId;
    }

    public void Execute(IModel model)
    {
        if (model.TryGetNode(nodeId, out var node)) return;
        if (Bounds != null) (BoundsCopy, node.Boundary) = (node.Boundary, Bounds);
        if (ContainerZoom != null) (ContainerZoomCopy, node.ContainerZoom) = (node.ContainerZoom, (double)ContainerZoom);
        if (ContainerOffset != null) (ContainerOffsetCopy, node.ContainerOffset) = (node.ContainerOffset, ContainerOffset);
    }

    public void Unexecute(IModel model)
    {
        if (model.TryGetNode(nodeId, out var node)) return;
        if (BoundsCopy != null) (Bounds, node.Boundary) = (node.Boundary, BoundsCopy);
        if (ContainerZoomCopy != null) (ContainerZoom, node.ContainerZoom) = (node.ContainerZoom, (double)ContainerZoomCopy);
        if (ContainerOffsetCopy != null) (ContainerOffset, node.ContainerOffset) = (node.ContainerOffset, ContainerOffsetCopy);
    }
}
