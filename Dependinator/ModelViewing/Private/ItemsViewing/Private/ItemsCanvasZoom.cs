using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Input;


namespace Dependinator.ModelViewing.Private.ItemsViewing
{
	internal class ItemsCanvasZoom
	{
		private readonly ItemsCanvas itemsCanvas;


		public ItemsCanvasZoom(ItemsCanvas itemsCanvas)
		{
			this.itemsCanvas = itemsCanvas;
		}


		public void ZoomRoot(MouseWheelEventArgs e) => Zoom(itemsCanvas.RootCanvas, e);

		public void ZoomNode(MouseWheelEventArgs e) => Zoom(itemsCanvas, e);


		private static void Zoom(ItemsCanvas itemsCanvas, MouseWheelEventArgs e)
		{
			double zoomFactor = ZoomFactor(e);
			Point zoomCenter = ZoomCenter(itemsCanvas, e);

			Zoom(itemsCanvas, zoomFactor, zoomCenter);
		}



		private static void Zoom(ItemsCanvas itemsCanvas, double zoom, Point zoomCenter)
		{
			double newScale = itemsCanvas.Scale * zoom;

			if (!IsValidZoomScale(zoom, newScale)) return;

			if (itemsCanvas.IsRoot) itemsCanvas.rootScale = newScale;
			else itemsCanvas.ScaleFactor = newScale / itemsCanvas.ParentCanvas.Scale;

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
			itemsCanvas.itemsSource.GetAllItems().Cast<ItemViewModel>().ForEach(item => item.NotifyAll());


		private static void UpdateAndNotifyAll(ItemsCanvas itemsCanvas)
		{
			IReadOnlyList<ItemViewModel> items = itemsCanvas.itemsSource.GetAllItems().Cast<ItemViewModel>().ToList();

			itemsCanvas.itemsSource.Update(items);

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
	}
}