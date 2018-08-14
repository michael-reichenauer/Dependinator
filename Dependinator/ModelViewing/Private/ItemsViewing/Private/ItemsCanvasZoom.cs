using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Input;
using Dependinator.Utils.UI.VirtualCanvas;


namespace Dependinator.ModelViewing.Private.ItemsViewing.Private
{
    internal class ItemsCanvasZoom
    {
        private readonly ItemsCanvas itemsCanvas;


        public ItemsCanvasZoom(ItemsCanvas itemsCanvas)
        {
            this.itemsCanvas = itemsCanvas;
        }


        public void Zoom(MouseWheelEventArgs e) => Zoom(itemsCanvas, e);


        public void ZoomRoot(double zoomFactor)
        {
            ItemsCanvas rootCanvas = itemsCanvas.RootCanvas;
            Point zoomCenter = ZoomCenter(rootCanvas);

            Zoom(rootCanvas, zoomFactor, zoomCenter, false);
        }


        public void ZoomNode(double zoomFactor, Point zoomCenter) =>
            Zoom(itemsCanvas, zoomFactor, zoomCenter, false);


        public void ZoomNode(double zoomFactor)
        {
            Point zoomCenter = ZoomCenter(itemsCanvas);
            Zoom(itemsCanvas, zoomFactor, zoomCenter, false);
        }


        public void UpdateScale()
        {
            if (itemsCanvas.ZoomableCanvas == null) return;

            SetZoomableCanvasScale(itemsCanvas);
            UpdateAndNotifyAll(itemsCanvas);
            ZoomDescendants(itemsCanvas);
        }


        private static void Zoom(ItemsCanvas itemsCanvas, MouseWheelEventArgs e)
        {
            double zoomFactor = ZoomFactor(e);
            Point zoomCenter = ZoomCenter(itemsCanvas, e);

            Zoom(itemsCanvas, zoomFactor, zoomCenter, true);
        }


        private static void Zoom(
            ItemsCanvas itemsCanvas, double zoomFactor, Point zoomCenter, bool isManual)
        {
            double newScale = itemsCanvas.Scale * zoomFactor;

            if (isManual && !IsValidZoomScale(zoomFactor, newScale)) return;

            if (itemsCanvas.IsRoot) itemsCanvas.RootScale = newScale;
            else itemsCanvas.ScaleFactor = newScale / itemsCanvas.ParentCanvas.Scale;

            UpdateScale(itemsCanvas, zoomCenter);
        }


        private static void UpdateScale(ItemsCanvas itemsCanvas, Point zoomCenter)
        {
            SetZoomableCanvasScale(itemsCanvas, zoomCenter);
            UpdateAndNotifyAll(itemsCanvas);
            ZoomDescendants(itemsCanvas);
        }


        private static void ZoomDescendants(ItemsCanvas itemsCanvas)
        {
            itemsCanvas.Descendants()
                .Where(item => item.IsShowing && item.CanShow && item.ZoomableCanvas != null)
                .ForEach(item =>
                {
                    SetZoomableCanvasScale(item);
                    NotifyAllItems(item);
                });
        }


        private static void NotifyAllItems(ItemsCanvas itemsCanvas) =>
            itemsCanvas.ItemsSource.GetAllItems().Cast<ItemViewModel>().ForEach(item => item.NotifyAll());


        private static void UpdateAndNotifyAll(ItemsCanvas itemsCanvas)
        {
            IReadOnlyList<ItemViewModel> items = itemsCanvas.ItemsSource.GetAllItems().Cast<ItemViewModel>().ToList();

            itemsCanvas.ItemsSource.Update(items);

            items.ForEach(item => item.NotifyAll());
        }


        private static void SetZoomableCanvasScale(ItemsCanvas itemsCanvas) =>
            itemsCanvas.ZoomableCanvas.Scale = itemsCanvas.Scale;


        private static void SetZoomableCanvasScale(ItemsCanvas itemsCanvas, Point zoomCenter)
        {
            // Adjust the offset to make the point at the center of zoom area stay still
            double zoomFactor = itemsCanvas.Scale / itemsCanvas.ZoomableCanvas.Scale;
            Vector position = (Vector)zoomCenter;

            Vector moveOffset = position * zoomFactor - position;
            itemsCanvas.MoveAllItems(-moveOffset);

            itemsCanvas.ZoomableCanvas.Scale = itemsCanvas.Scale;
        }


        private static double ZoomFactor(MouseWheelEventArgs e) => Math.Pow(2, e.Delta / 2000.0);


        private static bool IsValidZoomScale(double zoom, double newScale) => newScale > 0.40 || zoom > 1;


        private static Point ZoomCenter(ItemsCanvas itemsCanvas, MouseWheelEventArgs e)
        {
            Point viewPosition = e.GetPosition(itemsCanvas.ZoomableCanvas);

            return viewPosition + (Vector)itemsCanvas.ZoomableCanvas.Offset;
        }


        private static Point ZoomCenter(ItemsCanvas itemsCanvas)
        {
            ZoomableCanvas zoomableCanvas = itemsCanvas.ZoomableCanvas;
            Point viewCenter = new Point(zoomableCanvas.ActualWidth / 2.0, zoomableCanvas.ActualHeight / 2.0);
            Point zoomCenter = viewCenter + (Vector)zoomableCanvas.Offset;

            if (itemsCanvas.IsRoot)
            {
                // Compensate for root center offset to windows
                zoomCenter -= new Vector(10, 10);
            }

            return zoomCenter;
        }
    }
}
