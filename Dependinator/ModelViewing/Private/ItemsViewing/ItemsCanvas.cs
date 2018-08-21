using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Input;
using Dependinator.ModelViewing.Private.ItemsViewing.Private;
using Dependinator.ModelViewing.Private.Nodes;
using Dependinator.Utils;
using Dependinator.Utils.UI.VirtualCanvas;


namespace Dependinator.ModelViewing.Private.ItemsViewing
{
    internal class ItemsCanvas : IItemsCanvas
    {
        public static readonly double DefaultScaleFactor = 1.0 / 7.0;
        private static readonly Vector ChildOffset = new Vector(3, 3);

        private readonly ItemsCanvasZoom itemsCanvasZoom;
        private readonly IItemsCanvasOwner owner;
        private bool isFocused;


        private Point rootOffset;


        // The root canvas
        public ItemsCanvas()
            : this(null, null)
        {
        }


        public ItemsCanvas(IItemsCanvasOwner owner, ItemsCanvas parentCanvas)
        {
            this.owner = owner;
            ParentCanvas = parentCanvas;
            itemsCanvasZoom = new ItemsCanvasZoom(this);

            RootCanvas = parentCanvas?.RootCanvas ?? this;

            VisualAreaHandler visualAreaHandler = new VisualAreaHandler(this);
            ItemsSource = new ItemsSource(visualAreaHandler);

            if (IsRoot)
            {
                // Creating root node canvas
                RootScale = 1;
                ScaleFactor = 1;
                isFocused = true;
            }
            else
            {
                // Creating child node canvas
                ScaleFactor = DefaultScaleFactor;
                parentCanvas?.CanvasChildren.Add(this);
            }
        }


        public bool IsShowing => owner?.IsShowing ?? true;
        public bool CanShow => owner?.CanShow ?? true;

        public ItemsSource ItemsSource { get; }
        public double RootScale { get; set; }
        public List<ItemsCanvas> CanvasChildren { get; } = new List<ItemsCanvas>();
        public double ScaleFactor { get; set; }
        public double Scale => ParentCanvas?.Scale * ScaleFactor ?? RootScale;
        public ZoomableCanvas ZoomableCanvas { get; private set; }
        public ItemsCanvas ParentCanvas { get; private set; }
        public ItemsCanvas RootCanvas { get; }
        public bool IsRoot => ParentCanvas == null;
        public bool IsZoomAndMoveEnabled { get; set; } = true;

        public Rect ItemsCanvasBounds => owner?.ItemBounds ?? ZoomableCanvas?.ActualViewbox ?? Rect.Empty;
        public Point RootOffset => RootCanvas.ZoomableCanvas?.Offset ?? RootCanvas.rootOffset;
        public void SetRootOffset(Point offset) => RootCanvas.rootOffset = offset;
        public void SetRootScale(double scale) => RootCanvas.RootScale = scale;


        public bool IsFocused
        {
            get => isFocused;
            set
            {
                isFocused = value;

                // Root canvas is focused if no other canvas is focused
                RootCanvas.isFocused = isFocused ? IsRoot : !IsRoot;
            }
        }


        public void AddItem(IItem item)
        {
            item.ItemOwnerCanvas = this;
            ItemsSource.Add(item);
        }


        public void RemoveItem(IItem item) => ItemsSource.Remove(item);


        public void RemoveAll()
        {
            ItemsSource.RemoveAll();
            CanvasChildren.Clear();
        }


        public void UpdateItem(IItem item) => ItemsSource.Update(item);


        public void SizeChanged() => ItemsSource.TriggerExtentChanged();


        public void RemoveChildCanvas(ItemsCanvas childCanvas)
        {
            childCanvas.ParentCanvas = null;
            CanvasChildren.Remove(childCanvas);
        }


        public void CanvasRealized() => itemsCanvasZoom.UpdateScale();


        public void CanvasVirtualized()
        {
        }


        public void UpdateAll() => itemsCanvasZoom.ZoomRoot(1);


        public void Zoom(MouseWheelEventArgs e)
        {
            if (!IsZoomAndMoveEnabled) return;

            itemsCanvasZoom.Zoom(e);

            e.Handled = true;
        }


        public void ZoomRoot(double zoomFactor) => itemsCanvasZoom.ZoomRoot(zoomFactor);


        public void UpdateAndNotifyAll(bool isUpdate)
        {
            IReadOnlyList<ItemViewModel> items = ItemsSource.GetAllItems().Cast<ItemViewModel>().ToList();
            if (isUpdate)
            {
                ItemsSource.Update(items);
            }

            items.ForEach(item => item.NotifyAll());
        }


        public bool IsNodeInViewBox(Rect bounds)
        {
            Rect viewBox = GetViewBox();

            return viewBox.IntersectsWith(bounds);
        }


        public void MoveAllItems(Point sp1, Point sp2)
        {
            if (!IsZoomAndMoveEnabled)
            {
                return;
            }

            Point cp1 = ScreenToCanvasPoint(sp1);
            Point cp2 = ScreenToCanvasPoint(sp2);

            Vector moveOffset = cp2 - cp1;

            MoveCanvasItems(moveOffset);
        }


        public void MoveAllItems(Vector viewOffset)
        {
            if (!IsZoomAndMoveEnabled)
            {
                return;
            }

            Vector moveOffset = new Vector(viewOffset.X / Scale, viewOffset.Y / Scale);


            MoveCanvasItems(moveOffset);
        }


        public void UpdateScale()
        {
            if (!IsShowing || !CanShow)
            {
                // Item not shown or will be hidden
                return;
            }

            itemsCanvasZoom.UpdateScale();
        }


        public Point MouseToCanvasPoint()
        {
            Point screenPoint = Mouse.GetPosition(ZoomableCanvas);

            return ZoomableCanvas.GetCanvasPoint(screenPoint);
        }


        public Point MouseEventToCanvasPoint(MouseEventArgs e)
        {
            Point screenPoint = e.GetPosition(ZoomableCanvas);

            return ZoomableCanvas.GetCanvasPoint(screenPoint);
        }


        public Point CanvasToScreenPoint(Point canvasPoint)
        {
            try
            {
                Point localScreenPoint = ZoomableCanvas.GetVisualPoint(canvasPoint);

                Point screenPoint = ZoomableCanvas.PointToScreen(localScreenPoint);

                return screenPoint;
            }
            catch (Exception e)
            {
                Log.Exception(e, $"Node {this}");
                throw;
            }
        }


        public Point ScreenToCanvasPoint(Point screenPoint)
        {
            try
            {
                Point localScreenPoint = ZoomableCanvas.PointFromScreen(screenPoint);

                Point canvasPoint = ZoomableCanvas.GetCanvasPoint(localScreenPoint);

                return canvasPoint;
            }
            catch (Exception e)
            {
                Log.Exception(e, $"Node {this}");
                throw;
            }
        }


        public Point ParentToChildCanvasPoint(Point parentCanvasPoint)
        {
            if (!IsRoot)
            {
                Point relativeParentPoint = parentCanvasPoint - (Vector)ItemsCanvasBounds.Location;

                // Point within the parent node
                Point compensatedPoint = (Point)((Vector)relativeParentPoint / ScaleFactor);

                return compensatedPoint;
            }

            return parentCanvasPoint;
        }


        public Point ChildToParentCanvasPoint(Point childCanvasPoint)
        {
            if (!IsRoot)
            {
                // Point within the parent node
                Vector parentPoint = (Vector)childCanvasPoint * ScaleFactor;

                // point in parent canvas scale
                Point childToParentCanvasPoint = ItemsCanvasBounds.Location + parentPoint;
                return childToParentCanvasPoint;
            }

            return childCanvasPoint;
        }


        public Point CanvasToScreenPoint2(Point childCanvasPoint)
        {
            if (!IsRoot)
            {
                // Point within the parent node
                Vector parentPoint = (Vector)childCanvasPoint * ScaleFactor;

                // point in parent canvas scale
                Point childToParentCanvasPoint = ItemsCanvasBounds.Location + parentPoint;

                return ParentCanvas.CanvasToScreenPoint2(childToParentCanvasPoint) + ChildOffset;
            }

            return CanvasToScreenPoint(childCanvasPoint);
        }


        //public Point ParentToChildCanvasPoint2(Point parentCanvasPoint)
        //{
        //	Point parentScreenPoint = ParentCanvas.CanvasToScreenPoint(parentCanvasPoint);
        //	return ScreenToCanvasPoint(parentScreenPoint);
        //}


        public void SetZoomableCanvas(ZoomableCanvas canvas)
        {
            if (ZoomableCanvas != null)
            {
                // New canvas replacing previous canvas
                ZoomableCanvas.ItemRealized -= Canvas_ItemRealized;
                ZoomableCanvas.ItemVirtualized -= Canvas_ItemVirtualized;
            }

            ZoomableCanvas = canvas;
            if (!IsRoot)
            {
                ZoomableCanvas.IsDisableOffsetChange = true;
            }
            else
            {
                ZoomableCanvas.Offset = rootOffset;
            }

            ZoomableCanvas.ItemRealized += Canvas_ItemRealized;
            ZoomableCanvas.ItemVirtualized += Canvas_ItemVirtualized;
            ZoomableCanvas.ItemsOwner.ItemsSource = ItemsSource;

            ZoomableCanvas.Scale = Scale;
        }


        public override string ToString() => owner?.ToString() ?? NodeName.Root.ToString();


        public int ChildItemsCount() => ItemsSource.GetAllItems().Count();


        public int ShownChildItemsCount() => ItemsSource.GetAllItems().Count(item => item.IsShowing);


        public int DescendantsItemsCount()
        {
            int count = ItemsSource.GetAllItems().Count();

            count += CanvasChildren.Sum(canvas => canvas.DescendantsItemsCount());
            return count;
        }


        public int ShownDescendantsItemsCount()
        {
            int count = ItemsSource.GetAllItems().Count(item => item.IsShowing);

            count += CanvasChildren.Sum(canvas => canvas.ShownDescendantsItemsCount());
            return count;
        }

        public void ZoomNode(double zoomFactor, Point zoomCenter) => itemsCanvasZoom.ZoomNode(zoomFactor, zoomCenter);


        public IEnumerable<ItemsCanvas> Descendants()
        {
            Queue<ItemsCanvas> queue = new Queue<ItemsCanvas>();

            CanvasChildren.ForEach(queue.Enqueue);

            while (queue.Any())
            {
                ItemsCanvas descendant = queue.Dequeue();
                yield return descendant;

                descendant.CanvasChildren.ForEach(queue.Enqueue);
            }
        }


        private void TriggerInvalidated() => ItemsSource.TriggerInvalidated();


        private void MoveCanvasItems(Vector moveOffset)
        {
            //Rect viewBox = GetViewBox();

            //if (IsRoot && !IsAnyNodesWithinView(viewBox, moveOffset))
            //{
            //    // No node (if moved) would be withing visible view
            //    return;
            //}

            if (IsRoot)
            {
                ZoomableCanvas.Offset -= moveOffset * Scale;
            }
            else
            {
                ItemsSource.GetAllItems().ForEach(item => item.MoveItem(moveOffset));
            }

            UpdateAndNotifyAll(!IsRoot);
            TriggerInvalidated();
            UpdateShownItemsInChildren();
        }


        private Rect GetViewBox()
        {
            Rect viewBox = ZoomableCanvas.ActualViewbox;
            viewBox.Inflate(-30 / Scale, -30 / Scale);
            return viewBox;
        }


        private bool IsAnyNodesWithinView(Rect viewBox, Vector moveOffset)
        {
            IEnumerable<IItem> nodes = ItemsSource.GetAllItems().Where(i => i is NodeViewModel);
            foreach (IItem node in nodes)
            {
                if (node.CanShow)
                {
                    if (IsNodeInViewBox(node, viewBox, moveOffset))
                    {
                        return true;
                    }
                }
            }

            return false;
        }


        private static bool IsNodeInViewBox(IItem node, Rect viewBox, Vector moveOffset)
        {
            Point location = node.ItemBounds.Location;
            Size size = node.ItemBounds.Size;

            Rect newBounds = new Rect(location + moveOffset, size);
            if (viewBox.IntersectsWith(newBounds))
            {
                return true;
            }

            return false;
        }


        private void UpdateShownItemsInChildren()
        {
            CanvasChildren
                .Where(canvas => canvas.IsShowing)
                .ForEach(canvas =>
                {
                    canvas.TriggerInvalidated();
                    canvas.UpdateShownItemsInChildren();
                });
        }


        private void Canvas_ItemRealized(object sender, ItemEventArgs e)
        {
            ItemsSource.ItemRealized(e.VirtualId);
        }


        private void Canvas_ItemVirtualized(object sender, ItemEventArgs e)
        {
            ItemsSource.ItemVirtualized(e.VirtualId);
        }
    }
}
