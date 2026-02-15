using Dependinator.Models;

namespace Dependinator.Diagrams;

interface INodeEditService
{
    void MoveSelectedNode(PointerEvent e, double zoom, PointerId pointerId);
    void PanSelectedNode(PointerEvent e, double zoom, PointerId pointerId);
    void ResizeSelectedNode(PointerEvent e, double zoom, PointerId pointerId);
    void ZoomSelectedNode(PointerEvent e, PointerId pointerId);
    void PanZoomToFit(PointerId pointerId);
}

[Scoped]
class NodeEditService(IModelService modelService) : INodeEditService
{
    const double MaxZoom = 1.0;
    const double MinZoom = 1.0 / 10.0;
    const double Margin = 25;
    const double WheelZoomSpeed = 1.2;
    const double PinchZoomSpeed = 1.04;

    public void MoveSelectedNode(PointerEvent e, double zoom, PointerId pointerId)
    {
        modelService.UseNode(
            pointerId.Id,
            node =>
            {
                var nodeZoom = node.GetZoom() * zoom;
                var (dx, dy) = (e.MovementX * nodeZoom, e.MovementY * nodeZoom);
                var newBoundary = node.Boundary with { X = node.Boundary.X + dx, Y = node.Boundary.Y + dy };

                modelService.Do(new NodeEditCommand(node.Id) { Boundary = newBoundary });
            }
        );
    }

    public void PanSelectedNode(PointerEvent e, double zoom, PointerId pointerId)
    {
        modelService.UseNode(
            pointerId.Id,
            node =>
            {
                var nodeZoom = node.GetZoom() * zoom;
                var (dx, dy) = (e.MovementX * nodeZoom, e.MovementY * nodeZoom);
                var newContainerOffset = node.ContainerOffset with
                {
                    X = node.ContainerOffset.X + dx,
                    Y = node.ContainerOffset.Y + dy,
                };

                modelService.Do(new NodeEditCommand(node.Id) { ContainerOffset = newContainerOffset });
            }
        );
    }

    public void ResizeSelectedNode(PointerEvent e, double zoom, PointerId pointerId)
    {
        modelService.UseNode(
            pointerId.Id,
            node =>
            {
                var nodeZoom = node.GetZoom() * zoom;
                var (dx, dy) = (e.MovementX * nodeZoom, e.MovementY * nodeZoom);

                var oldBoundary = node.Boundary;
                var newBoundary = pointerId.NodeResizeType switch
                {
                    NodeResizeType.TopLeft => node.Boundary with
                    {
                        X = node.Boundary.X + dx,
                        Y = node.Boundary.Y + dy,
                        Width = node.Boundary.Width - dx,
                        Height = node.Boundary.Height - dy,
                    },
                    NodeResizeType.TopMiddle => node.Boundary with
                    {
                        Y = node.Boundary.Y + dy,
                        Height = node.Boundary.Height - dy,
                    },
                    NodeResizeType.TopRight => node.Boundary with
                    {
                        X = node.Boundary.X,
                        Y = node.Boundary.Y + dy,
                        Width = node.Boundary.Width + dx,
                        Height = node.Boundary.Height - dy,
                    },

                    NodeResizeType.MiddleLeft => node.Boundary with
                    {
                        X = node.Boundary.X + dx,
                        Width = node.Boundary.Width - dx,
                    },
                    NodeResizeType.MiddleRight => node.Boundary with
                    {
                        X = node.Boundary.X,
                        Width = node.Boundary.Width + dx,
                    },

                    NodeResizeType.BottomLeft => node.Boundary with
                    {
                        X = node.Boundary.X + dx,
                        Y = node.Boundary.Y,
                        Width = node.Boundary.Width - dx,
                        Height = node.Boundary.Height + dy,
                    },
                    NodeResizeType.BottomMiddle => node.Boundary with
                    {
                        Y = node.Boundary.Y,
                        Height = node.Boundary.Height + dy,
                    },
                    NodeResizeType.BottomRight => node.Boundary with
                    {
                        X = node.Boundary.X,
                        Y = node.Boundary.Y,
                        Width = node.Boundary.Width + dx,
                        Height = node.Boundary.Height + dy,
                    },

                    _ => node.Boundary,
                };

                // Adjust container offset to ensure that children stay in place
                var newContainerOffset = node.ContainerOffset with
                {
                    X = node.ContainerOffset.X - (newBoundary.X - oldBoundary.X),
                    Y = node.ContainerOffset.Y - (newBoundary.Y - oldBoundary.Y),
                };

                modelService.Do(
                    new NodeEditCommand(node.Id) { Boundary = newBoundary, ContainerOffset = newContainerOffset }
                );
            }
        );
    }

    public void ZoomSelectedNode(PointerEvent e, PointerId pointerId)
    {
        modelService.UseNode(
            pointerId.Id,
            node =>
            {
                if (e.DeltaY == 0)
                    return;
                //var (mx, my) = (e.OffsetX - node.ContainerOffset.X, e.OffsetY - node.ContainerOffset.Y);
                var (mx, my) = (node.Boundary.Width, node.Boundary.Height);
                // var (mx, my) = (node.Boundary.Width / 2 - node.ContainerOffset.X, node.Boundary.Height / 2 - node.ContainerOffset.Y);

                var speed = e.PointerType == "touch" ? PinchZoomSpeed : WheelZoomSpeed;
                double newZoom = (e.DeltaY < 0) ? node.ContainerZoom * speed : node.ContainerZoom * (1 / speed);
                if (newZoom < MinZoom)
                    newZoom = MinZoom;
                if (newZoom > MaxZoom)
                    newZoom = MaxZoom;

                double svgX = mx * node.ContainerZoom + node.ContainerOffset.X;
                double svgY = my * node.ContainerZoom + node.ContainerOffset.Y;

                var w = node.Boundary.Width * newZoom;
                var h = node.Boundary.Height * newZoom;

                var x = svgX - mx / node.Boundary.Width * w;
                var y = svgY - my / node.Boundary.Height * h;

                var newContainerOffset = new Pos(x, y);

                modelService.Do(
                    new NodeEditCommand(node.Id) { ContainerOffset = newContainerOffset, ContainerZoom = newZoom }
                );
            }
        );
    }

    public void PanZoomToFit(PointerId pointerId)
    {
        modelService.UseNode(
            pointerId.Id,
            node =>
            {
                using var model = modelService.UseModel();

                Rect b = node.GetTotalBounds();

                // Determine the X or y zoom that best fits the bounds (including margin)
                var zx = (b.Width + 2 * Margin) / node.Boundary.Width;
                var zy = (b.Height + 2 * Margin) / node.Boundary.Height;
                var newZoom = 1 / Math.Max(zx, zy);

                var wx = node.Boundary.Width * newZoom;
                var wy = node.Boundary.Height / newZoom;

                // Zoom width and height to fit the bounds
                var nw = (b.Width + 2 * Margin) * newZoom;
                var nh = (b.Height + 2 * Margin) * newZoom;
                var w = node.Boundary.Width * newZoom;
                var h = node.Boundary.Height * newZoom;

                // Pan to center the bounds
                var mx = Margin;
                var my = Margin;
                var x = -(b.X - mx) * newZoom;
                var y = -(b.Y - my) * newZoom;

                var newOffset = new Pos(x, y);

                modelService.Do(new NodeEditCommand(node.Id) { ContainerOffset = newOffset, ContainerZoom = newZoom });
            }
        );
    }
}
