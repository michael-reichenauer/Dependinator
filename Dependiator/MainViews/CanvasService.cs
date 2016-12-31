using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Input;
using Dependiator.MainViews.Private;
using Dependiator.Utils;
using Dependiator.Utils.UI.VirtualCanvas;


namespace Dependiator.MainViews
{
	[SingleInstance]
	internal class CanvasService : ICanvasService
	{
		private readonly IMainViewItemsSource itemsSource;

		private ZoomableCanvas canvas;


		public CanvasService(IMainViewItemsSource itemsSource)
		{
			this.itemsSource = itemsSource;
		}

		public void SetCanvas(ZoomableCanvas zoomableCanvas)
		{
			canvas = zoomableCanvas;
		}


		public bool ZoomCanvas(int zoomDelta, Point viewPosition)
		{
			// Adjust X in "zoomDelta / X" to adjust zoom speed
			double zoom = Math.Pow(2, zoomDelta / 10.0 / Mouse.MouseWheelDeltaForOneLine);

			double oldScale = canvas.Scale;
			double newScale = canvas.Scale * zoom;

			Log.Debug($"Zoom {zoom}, scale {canvas.Scale}, offset {canvas.Offset}");
			if (newScale < 0.1 || newScale > 5)
			{
				Log.Warn($"Zoom to large");
				return true;
			}

			Point canvasPosition = GetCanvasPosition(viewPosition);

			Point centerPosition = viewPosition;
			IVirtualItem item = null;

			for (int i = 1; i < 100; i = i + 5)
			{
				double nearSize = GetNearSize(oldScale, i);
				Rect nearArea = new Rect(
					canvasPosition.X - nearSize, canvasPosition.Y - nearSize, nearSize * 2, nearSize * 2);
				IEnumerable<IVirtualItem> itemsInArea = itemsSource.GetItemsInArea(nearArea);
				item = itemsInArea.FirstOrDefault(); 

				if (item != null)
				{
					Rect itemBounds = item.ItemBounds;
					canvasPosition = new Point(
						itemBounds.Left + (itemBounds.Width * zoom) / 2,
						itemBounds.Top + (itemBounds.Height * zoom) / 2);
					centerPosition = GetViewPosition(canvasPosition);

					break;
				}
			}

			if (item == null)
			{
				Log.Warn("Did not find item");
			}

			canvas.Scale = newScale;


			// Adjust the offset to make the point under the mouse stay still.
			Vector position = (Vector)centerPosition;
			canvas.Offset = (Point)((Vector)(canvas.Offset + position) * zoom - position);

			Log.Debug($"Scroll {zoom}, scale {canvas.Scale}, offset {canvas.Offset}");

			return true;
		}


		private static double GetNearSize(double scale, int size)
		{
			double scaledSize = (size / scale);
			return scaledSize == 0 ? 1 : scaledSize;
		}


		public bool MoveCanvas(Vector viewOffset)
		{
			canvas.Offset -= viewOffset;
			return true;
		}


		public Point GetCanvasPosition(Point viewPosition)
		{
			double x = viewPosition.X + canvas.Offset.X;
			double y = viewPosition.Y + canvas.Offset.Y;

			Point canvasPosition = new Point(x, y);
			return canvasPosition;
		}

		public Point GetViewPosition(Point canvasPosition)
		{
			double x = canvasPosition.X - canvas.Offset.X;
			double y = canvasPosition.Y - canvas.Offset.Y;

			Point viewPosition = new Point(x, y);
			return viewPosition;
		}

	}
}