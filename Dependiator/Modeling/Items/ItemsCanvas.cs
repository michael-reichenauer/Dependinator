using System;
using System.Collections.Generic;
using System.Windows;
using Dependiator.Utils;
using Dependiator.Utils.UI.VirtualCanvas;


namespace Dependiator.Modeling.Items
{
	internal class ItemsCanvas
	{
		private static readonly double ZoomSpeed = 1000.0;

		private readonly IItem item;
		private readonly ItemsSource itemsSource;

		private ZoomableCanvas zoomableCanvas;
		private double scale = 1.0;
		private Point offset = new Point(0, 0);


		public ItemsCanvas(IItem item, ItemsCanvas parentItemsCanvas)
		{
			this.item = item;
			ParentCanvas = parentItemsCanvas;
			itemsSource = new ItemsSource(this);
		}


		public Rect LastViewAreaQuery => itemsSource.LastViewAreaQuery;
		public Rect ItemBounds => item?.ItemBounds ?? zoomableCanvas?.ActualViewbox ?? Rect.Empty;
		public ItemsCanvas ParentCanvas { get; }


		public double ScaleFactor { get; private set; } = 1.0;
		//{
		//	get { return scaleFactor; }
		//	set
		//	{
		//		scaleFactor 


		//	}
		//}=> ParentCanvas?.scale / scale ?? 1.0;
		

		public Point Offset
		{
			get { return offset; }
			set
			{
				offset = value;

				if (zoomableCanvas != null)
				{
					zoomableCanvas.Offset = value;
				}

				//// Log.Debug("offset changed");
				//foreach (IItem item in itemsSource.GetItemsInView())
				//{			
				//	if (item is CompositeNodeViewModel compositeNodeViewModel)
				//	{
				//		//Log.Debug("item is CompositeNodeViewModel");
				//		compositeNodeViewModel.NodesViewModel.ItemsCanvas.TriggerInvalidated();
				//	}
				//}
			}
		}


		public double Scale
		{
			get { return scale; }
			set
			{
				scale = value;

				if (zoomableCanvas != null)
				{
					zoomableCanvas.Scale = value;
				}

				if (ParentCanvas != null)
				{
					ScaleFactor = ParentCanvas.scale / scale;
				}
			}
		}


		public void UpdateScale()
		{
			if (ParentCanvas != null)
			{
				Scale = ParentCanvas.scale / ScaleFactor;
			}			
		}


		public void SetCanvas(ZoomableCanvas canvas)
		{
			if (zoomableCanvas != null)
			{
				// New canvas replacing previous canvas
				zoomableCanvas.ItemRealized -= Canvas_ItemRealized;
				zoomableCanvas.ItemVirtualized -= Canvas_ItemVirtualized;
			}

			zoomableCanvas = canvas;

			zoomableCanvas.Scale = scale;
			zoomableCanvas.Offset = offset;

			zoomableCanvas.ItemsOwner.ItemsSource = itemsSource.VirtualItemsSource;

			zoomableCanvas.ItemRealized += Canvas_ItemRealized;
			zoomableCanvas.ItemVirtualized += Canvas_ItemVirtualized;
		}


		public void AddItem(IItem item) => itemsSource.Add(item);


		public void AddItems(IEnumerable<IItem> items) => itemsSource.Add(items);

		public void RemoveItem(IItem item) => itemsSource.Remove(item);


		public void UpdateItem(IItem item) => itemsSource.Update(item);


		public bool Zoom(int zoomDelta, Point viewPosition)
		{
			double zoom = Math.Pow(2, zoomDelta / ZoomSpeed);

			double newScale = Scale * zoom;
			zoom = newScale / Scale;

			Scale = newScale;

			// Adjust the offset to make the point under the mouse stay still.
			Vector position = (Vector)viewPosition;
			Offset = (Point)((Vector)(Offset + position) * zoom - position);

			// Log.Debug($"Scale: {canvas.Scale}");

			//TriggerScaleChanged();

			return true;
		}


		public bool Move(Vector viewOffset)
		{
			Offset -= viewOffset;
			return true;
		}



		public void TriggerExtentChanged()
		{
			itemsSource.TriggerExtentChanged();
		}

		public void TriggerInvalidated()
		{
			itemsSource.TriggerInvalidated();
		}


		//private void TriggerScaleChanged()
		//{
		//	ScaleChanged?.Invoke(this, EventArgs.Empty);
		//}


		public override string ToString() => item?.ToString() ?? "<root>";

		private void Canvas_ItemRealized(object sender, ItemEventArgs e)
		{
			itemsSource.ItemRealized(e.VirtualId);
		}


		private void Canvas_ItemVirtualized(object sender, ItemEventArgs e)
		{
			itemsSource.ItemVirtualized(e.VirtualId);
		}
	}
}