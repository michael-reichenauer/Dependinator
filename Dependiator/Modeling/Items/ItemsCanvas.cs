using System;
using System.Windows;
using Dependiator.Utils.UI.VirtualCanvas;


namespace Dependiator.Modeling.Items
{
	internal class ItemsCanvas
	{
		private static readonly double ZoomSpeed = 600.0;

		private readonly ItemsSource itemsSource = new ItemsSource();
		private ZoomableCanvas canvas;


		public void SetCanvas(ZoomableCanvas zoomableCanvas)
		{
			canvas = zoomableCanvas;
			canvas.ItemsOwner.ItemsSource = itemsSource.VirtualItemsSource;

			canvas.ItemRealized += (s, e) => itemsSource.ItemRealized(e.VirtualId);
			canvas.ItemVirtualized += (s, e) => itemsSource.ItemVirtualized(e.VirtualId);
		}


		public event EventHandler ScaleChanged;

		public Rect CurrentViewPort => canvas.ActualViewbox;

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


		public void AddItem(IItem item)
		{
			itemsSource.Add(item);
		}


		public bool Zoom(int zoomDelta, Point viewPosition)
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


		public bool Move(Vector viewOffset)
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


		public void TriggerExtentChanged()
		{
			itemsSource.TriggerExtentChanged();
		}


		private void TriggerScaleChanged()
		{
			ScaleChanged?.Invoke(this, EventArgs.Empty);
		}
	}
}