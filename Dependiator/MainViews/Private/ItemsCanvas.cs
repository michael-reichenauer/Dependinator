using System;
using System.Windows;
using Dependiator.Utils;
using Dependiator.Utils.UI.VirtualCanvas;


namespace Dependiator.MainViews.Private
{

	internal class ItemsCanvas
	{
		private static readonly double ZoomSpeed = 600.0;

		private ZoomableCanvas canvas;
		private readonly NodeItemsSource nodeItemsSource = new NodeItemsSource();

		public Rect CurrentViewPort => canvas.ActualViewbox;

		public event EventHandler ScaleChanged;



		public void SetCanvas(ZoomableCanvas zoomableCanvas)
		{
			canvas = zoomableCanvas;
			canvas.ItemsOwner.ItemsSource = nodeItemsSource.VirtualItemsSource;

			canvas.ItemRealized += (s, e) => nodeItemsSource.ItemRealized(e.VirtualId);
			canvas.ItemVirtualized += (s, e) => nodeItemsSource.ItemVirtualized(e.VirtualId);
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


		public void TriggerExtentChanged()
		{
			nodeItemsSource.TriggerExtentChanged();
		}


		public void AddItem(IItem item)
		{
			nodeItemsSource.Add(item);
		}
	}
}