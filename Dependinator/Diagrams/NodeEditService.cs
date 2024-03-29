using Dependinator.Models;
using Dependinator.Utils.UI;

namespace Dependinator.Diagrams;

interface INodeEditService
{
    void MoveSelectedNode(MouseEvent e, double zoom, string mouseDownId);
    void ResizeSelectedNode(MouseEvent e, double zoom, string mouseDownId, string mouseDownSubId);
}

[Scoped]
class NodeEditService : INodeEditService
{
    readonly IModelService modelService;

    public NodeEditService(IModelService modelService)
    {
        this.modelService = modelService;
    }

    public void MoveSelectedNode(MouseEvent e, double zoom, string mouseDownId)
    {
        modelService.TryUpdateNode(mouseDownId, node =>
        {
            var nodeZoom = node.GetZoom() * zoom;
            var (dx, dy) = (e.MovementX * nodeZoom, e.MovementY * nodeZoom);

            node.Boundary = node.Boundary with { X = node.Boundary.X + dx, Y = node.Boundary.Y + dy };
        });
    }

    public void ResizeSelectedNode(MouseEvent e, double zoom, string mouseDownId, string mouseDownSubId)
    {
        modelService.TryUpdateNode(mouseDownId, node =>
        {
            var nodeZoom = node.GetZoom() * zoom;
            var (dx, dy) = (e.MovementX * nodeZoom, e.MovementY * nodeZoom);

            var oldBoundary = node.Boundary;
            node.Boundary = mouseDownSubId switch
            {
                "tl" => node.Boundary with { X = node.Boundary.X + dx, Y = node.Boundary.Y + dy, Width = node.Boundary.Width - dx, Height = node.Boundary.Height - dy },
                "tm" => node.Boundary with { Y = node.Boundary.Y + dy, Height = node.Boundary.Height - dy },
                "tr" => node.Boundary with { X = node.Boundary.X, Y = node.Boundary.Y + dy, Width = node.Boundary.Width + dx, Height = node.Boundary.Height - dy },

                "ml" => node.Boundary with { X = node.Boundary.X + dx, Width = node.Boundary.Width - dx },
                "mr" => node.Boundary with { X = node.Boundary.X, Width = node.Boundary.Width + dx },

                "bl" => node.Boundary with { X = node.Boundary.X + dx, Y = node.Boundary.Y, Width = node.Boundary.Width - dx, Height = node.Boundary.Height + dy },
                "bm" => node.Boundary with { Y = node.Boundary.Y, Height = node.Boundary.Height + dy },
                "br" => node.Boundary with { X = node.Boundary.X, Y = node.Boundary.Y, Width = node.Boundary.Width + dx, Height = node.Boundary.Height + dy },

                _ => node.Boundary
            };
            var newBoundary = node.Boundary;

            // Adjust container offest to ensure that children stay in place
            node.ContainerOffset = node.ContainerOffset with
            {
                X = node.ContainerOffset.X - (newBoundary.X - oldBoundary.X),
                Y = node.ContainerOffset.Y - (newBoundary.Y - oldBoundary.Y)
            };
        });
    }
}