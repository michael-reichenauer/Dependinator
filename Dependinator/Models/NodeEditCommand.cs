namespace Dependinator.Models;


class NodeEditCommand : Command
{
    readonly NodeId nodeId;

    public Rect? Boundary { get; set; }
    public Rect? BoundaryCopy { get; set; }
    public Double? ContainerZoom { get; set; }
    public Double? ContainerZoomCopy { get; set; }
    public Pos? ContainerOffset { get; set; }
    public Pos? ContainerOffsetCopy { get; set; }


    public NodeEditCommand(NodeId nodeId)
    {
        this.nodeId = nodeId;
    }


    public override void Execute(IModel model)
    {
        if (!model.TryGetNode(nodeId, out var node)) return;

        if (Boundary != null) (BoundaryCopy, node.Boundary) = (node.Boundary, Boundary);
        if (ContainerZoom != null) (ContainerZoomCopy, node.ContainerZoom) = (node.ContainerZoom, (double)ContainerZoom);
        if (ContainerOffset != null) (ContainerOffsetCopy, node.ContainerOffset) = (node.ContainerOffset, ContainerOffset);
    }


    public override void Unexecute(IModel model)
    {
        if (!model.TryGetNode(nodeId, out var node)) return;

        if (BoundaryCopy != null) (Boundary, node.Boundary, BoundaryCopy) = (node.Boundary, BoundaryCopy, null);
        if (ContainerZoomCopy != null) (ContainerZoom, node.ContainerZoom, ContainerZoomCopy) = (node.ContainerZoom, (double)ContainerZoomCopy, null);
        if (ContainerOffsetCopy != null) (ContainerOffset, node.ContainerOffset, ContainerOffsetCopy) = (node.ContainerOffset, ContainerOffsetCopy, null);
    }
}
