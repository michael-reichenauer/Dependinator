using System;
using System.Windows;
using Dependiator.Utils;
using Dependiator.Utils.UI.VirtualCanvas;


namespace Dependiator.MainViews.Private
{
	[SingleInstance]
	internal class CanvasService : ICanvasService
	{
		private readonly INodeItemsSource itemsSource;
		private static readonly double ZoomSpeed = 600.0;

		private ZoomableCanvas canvas;


		public Rect CurrentViewPort => canvas.ActualViewbox;

		public event EventHandler ScaleChanged;


		public CanvasService(INodeItemsSource itemsSource)
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

		public Point Offset
		{
			get { return canvas?.Offset ?? new Point(0, 0); }
			set { canvas.Offset = value; }
		}


		public double Scale
		{
			get { return canvas?.Scale ?? 1; }
			set
			{
				if (canvas != null)
				{
					canvas.Scale = value;
				}
			}
		}


		public bool ZoomCanvas(int zoomDelta, Point viewPosition)
		{		
			double zoom = Math.Pow(2, zoomDelta / ZoomSpeed);

			double newScale = canvas.Scale * zoom;
			zoom = newScale / canvas.Scale;

			canvas.Scale = newScale;

			// Adjust the offset to make the point under the mouse stay still.
			Vector position = (Vector)viewPosition;
			canvas.Offset = (Point)((Vector)(canvas.Offset + position) * zoom - position);

			// Log.Debug($"Scale: {canvas.Scale}");

			TriggerScaleChanged();

			return true;
		}


		private void TriggerScaleChanged()
		{
			ScaleChanged?.Invoke(this, EventArgs.Empty);
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