using System;
using System.Collections.Generic;
using System.Windows;
using Dependiator.Utils;
using Dependiator.Utils.UI.VirtualCanvas;


namespace Dependiator.Modeling.Items
{
	internal class ItemsCanvas
	{
		private static readonly double ZoomSpeed = 600.0;

		private readonly IItem item;
		private readonly ItemsSource itemsSource;
		
		private ZoomableCanvas canvas;
		private double scale = 1.0;

		public ItemsCanvas(IItem item, ItemsCanvas parentCanvas)
		{
			this.item = item;
			ParentCanvas = parentCanvas;
			itemsSource = new ItemsSource(this);
		}

		public Rect LastViewAreaQuery { get; set; }


		public void SetCanvas(ZoomableCanvas zoomableCanvas)
		{
			if (canvas != null)
			{
				canvas.ItemRealized -= Canvas_ItemRealized;
				canvas.ItemVirtualized -= Canvas_ItemVirtualized;
			}

			canvas = zoomableCanvas;
			canvas.Scale = scale;
			canvas.ItemsOwner.ItemsSource = itemsSource.VirtualItemsSource;

			canvas.ItemRealized += Canvas_ItemRealized;
			canvas.ItemVirtualized += Canvas_ItemVirtualized;
		}


		private void Canvas_ItemRealized(object sender, ItemEventArgs e)
		{
			itemsSource.ItemRealized(e.VirtualId);
		}


		private void Canvas_ItemVirtualized(object sender, ItemEventArgs e)
		{
			itemsSource.ItemVirtualized(e.VirtualId);
		}


		public Rect ItemBounds => item?.ItemBounds ?? canvas?.ActualViewbox ?? Rect.Empty;

		public double ScaleFactor => item?.ItemsScaleFactor ?? 1;

		public ItemsCanvas ParentCanvas { get; }

		public event EventHandler ScaleChanged;

		public Rect CurrentViewPort => canvas.ActualViewbox;

		public Point GetCanvasPoint(Point screenPoint) => canvas.GetCanvasPoint(screenPoint);

		public Point Offset
		{
			get { return canvas?.Offset ?? new Point(0, 0); }
			set
			{
				canvas.Offset = value;
				// Log.Debug("offset changed");
				foreach (IItem item in itemsSource.GetItemsInView())
				{			
					if (item is CompositeNodeViewModel compositeNodeViewModel)
					{
						//Log.Debug("item is CompositeNodeViewModel");
						compositeNodeViewModel.NodesViewModel.ItemsCanvas.TriggerInvalidated();
					}
				}
			}
		}


		public double Scale
		{
			get { return canvas?.Scale ?? 1; }
			set
			{
				if (canvas != null)
				{
					canvas.Scale = value;
					scale = value;
				}
				else
				{
					scale = value;
				}
			}
		}

		public void AddItem(IItem item) => itemsSource.Add(item);


		public void AddItems(IEnumerable<IItem> items) => itemsSource.Add(items);

		public void RemoveItem(IItem item) => itemsSource.Remove(item);


		public void UpdateItem(IItem item) => itemsSource.Update(item);


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

		public void TriggerInvalidated()
		{
			itemsSource.TriggerInvalidated();
		}


		private void TriggerScaleChanged()
		{
			ScaleChanged?.Invoke(this, EventArgs.Empty);
		}
	}
}