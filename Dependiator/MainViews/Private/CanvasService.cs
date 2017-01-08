using System;
using System.Windows;
using Dependiator.Utils;
using Dependiator.Utils.UI.VirtualCanvas;


namespace Dependiator.MainViews.Private
{
	[SingleInstance]
	internal class CanvasService : ICanvasService
	{
		private readonly IMainViewItemsSource itemsSource;
		private static readonly double ZoomSpeed = 600.0;

		private ZoomableCanvas canvas;


		public event EventHandler ScaleChanged;


		public CanvasService(IMainViewItemsSource itemsSource)
		{
			this.itemsSource = itemsSource;
		}


		public void SetCanvas(ZoomableCanvas zoomableCanvas)
		{
			canvas = zoomableCanvas;
			canvas.ItemRealized += (s, e) => itemsSource.ItemRealized(e.VirtualId);
			canvas.ItemVirtualized += (s, e) => itemsSource.ItemVirtualized(e.VirtualId);
		}


		public Point GetCanvasPoint(Point screenPoint) => canvas.GetCanvasPoint(screenPoint);

		public Point Offset => canvas?.Offset ?? new Point(0, 0);


		public double Scale => canvas?.Scale ?? 1;


		public bool ZoomCanvas(int zoomDelta, Point viewPosition)
		{		
			double zoom = Math.Pow(2, zoomDelta / ZoomSpeed);

			//double maxScale = 20;
			//double minScale = 0.01;

			double newScale = canvas.Scale * zoom;

			//// Limit zooming
			//if (newScale < minScale)
			//{
			//	newScale = minScale;			
			//}
			//else if (newScale > maxScale)
			//{
			//	newScale = maxScale;
			//}

			//if (newScale == canvas.Scale)
			//{
			//	return true;
			//}

			zoom = newScale / canvas.Scale;

			canvas.Scale = newScale;

			// Adjust the offset to make the point under the mouse stay still.
			Vector position = (Vector)viewPosition;
			canvas.Offset = (Point)((Vector)(canvas.Offset + position) * zoom - position);

			Log.Debug($"Scale: {canvas.Scale}");

			TriggerScaleChanged();

			return true;
		}


		private void TriggerScaleChanged()
		{
			ScaleChanged?.Invoke(this, EventArgs.Empty);
		}


		private double GetMinScale()
		{
			double minScaleWidth = canvas.ActualWidth / canvas.Extent.Width;
			double minScaleHeight = canvas.ActualHeight / canvas.Extent.Height;
			double minScale = Math.Min(minScaleWidth, minScaleHeight);
			return minScale;
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