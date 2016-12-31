using System;
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


		public double Scale => canvas.Scale;

		public bool ZoomCanvas(int zoomDelta, Point viewPosition)
		{
			// Adjust X in "zoomDelta / X" to adjust zoom speed
			double zoom = Math.Pow(2, zoomDelta / 10.0 / Mouse.MouseWheelDeltaForOneLine);

			double newScale = canvas.Scale * zoom;

			//Log.Debug($"Zoom {zoom}, scale {canvas.Scale}, offset {canvas.Offset}");
			//if (newScale < 0.3 || newScale > 20)
			//{
			//	return true;
			//}

			if (zoomDelta < 0 && canvas.ActualViewbox.Width >= canvas.Extent.Width
				&& canvas.ActualViewbox.Height > canvas.Extent.Height
				|| newScale > 20)
			{
				return true;
			}

			canvas.Scale = newScale;

			// Adjust the offset to make the point under the mouse stay still.
			Vector position = (Vector)viewPosition;
			canvas.Offset = (Point)((Vector)(canvas.Offset + position) * zoom - position);

			// Log.Debug($"Scroll {zoom}, scale {canvas.Scale}, offset {canvas.Offset}");

			itemsSource.GetItemsInView().ForEach(item => item.ZoomChanged());

			return true;
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