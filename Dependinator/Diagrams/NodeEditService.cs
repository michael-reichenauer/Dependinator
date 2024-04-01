using Dependinator.Models;

namespace Dependinator.Diagrams;

interface INodeEditService
{
    void MoveSelectedNode(PointerEvent e, double zoom, PointerId pointerId);
    void PanSelectedNode(PointerEvent e, double zoom, PointerId pointerId);
    void ResizeSelectedNode(PointerEvent e, double zoom, PointerId pointerId);
    void ZoomSelectedNode(PointerEvent e, PointerId pointerId);
}

[Scoped]
class NodeEditService(IModelService modelService) : INodeEditService
{
    const double MaxZoom = 10;
    const double WheelZoomSpeed = 1.2;
    const double PinchZoomSpeed = 1.04;

    public void MoveSelectedNode(PointerEvent e, double zoom, PointerId pointerId)
    {
        modelService.UseNode(pointerId.Id, node =>
        {
            var nodeZoom = node.GetZoom() * zoom;
            var (dx, dy) = (e.MovementX * nodeZoom, e.MovementY * nodeZoom);
            var newBoundary = node.Boundary with { X = node.Boundary.X + dx, Y = node.Boundary.Y + dy };

            modelService.Do(new NodeEditCommand(node.Id) { Boundary = newBoundary });
        });
    }

    public void PanSelectedNode(PointerEvent e, double zoom, PointerId pointerId)
    {
        modelService.UseNode(pointerId.Id, node =>
        {
            var nodeZoom = node.GetZoom() * zoom;
            var (dx, dy) = (e.MovementX * nodeZoom, e.MovementY * nodeZoom);

            node.ContainerOffset = node.ContainerOffset with { X = node.ContainerOffset.X + dx, Y = node.ContainerOffset.Y + dy };
        });
    }

    public void ResizeSelectedNode(PointerEvent e, double zoom, PointerId pointerId)
    {
        modelService.UseNode(pointerId.Id, node =>
        {
            var nodeZoom = node.GetZoom() * zoom;
            var (dx, dy) = (e.MovementX * nodeZoom, e.MovementY * nodeZoom);

            var oldBoundary = node.Boundary;
            node.Boundary = pointerId.SubId switch
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

    public void ZoomSelectedNode(PointerEvent e, PointerId pointerId)
    {
        modelService.UseNode(pointerId.Id, node =>
       {
           if (e.DeltaY == 0) return;
           //var (mx, my) = (e.OffsetX - node.ContainerOffset.X, e.OffsetY - node.ContainerOffset.Y);
           var (mx, my) = (node.Boundary.Width, node.Boundary.Height);
           // var (mx, my) = (node.Boundary.Width / 2 - node.ContainerOffset.X, node.Boundary.Height / 2 - node.ContainerOffset.Y);

           var speed = e.PointerType == "touch" ? PinchZoomSpeed : WheelZoomSpeed;
           double newZoom = (e.DeltaY < 0) ? node.ContainerZoom * speed : node.ContainerZoom * (1 / speed);
           //if (newZoom > MaxZoom) newZoom = MaxZoom;

           double svgX = mx * node.ContainerZoom + node.ContainerOffset.X;
           double svgY = my * node.ContainerZoom + node.ContainerOffset.Y;

           var w = node.Boundary.Width * newZoom;
           var h = node.Boundary.Height * newZoom;

           var x = svgX - mx / node.Boundary.Width * w;
           var y = svgY - my / node.Boundary.Height * h;

           node.ContainerOffset = new Pos(x, y);
           node.ContainerZoom = newZoom;
       });
    }
}