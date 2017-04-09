using System;
using System.Collections.Generic;
using System.Windows;
using Dependiator.Utils.UI.VirtualCanvas;


namespace Dependiator.Modeling.Items
{
	internal class ItemsCanvas
	{
		private static readonly double ZoomSpeed = 1000.0;

		private readonly IItem ownerItem;
		private readonly ItemsSource itemsSource;


		// ######## public !!!!!!!!!!!!!!!!!!!!!!!!!!!
		public ZoomableCanvas zoomableCanvas;
		private double scale = 1.0;
		private Point offset = new Point(0, 0);


		public ItemsCanvas(IItem ownerItem, ItemsCanvas parentItemsCanvas)
		{
			this.ownerItem = ownerItem;
			ParentItemsCanvas = parentItemsCanvas;
			itemsSource = new ItemsSource(this);
		}


		public Rect LastViewAreaQuery => itemsSource.LastViewAreaQuery;
		public Rect ItemBounds => ownerItem?.ItemBounds ?? zoomableCanvas?.ActualViewbox ?? Rect.Empty;
		public ItemsCanvas ParentItemsCanvas { get; }


		public double ScaleFactor { get; private set; } = 1.0;



		public Point Offset
		{
			get => offset;
			set
			{
				offset = value;
				if (zoomableCanvas != null)
				{
					zoomableCanvas.Offset = value;
				}
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

				if (ParentItemsCanvas != null)
				{
					ScaleFactor = ParentItemsCanvas.scale / scale;
				}
			}
		}



		public void UpdateScale()
		{
			if (ParentItemsCanvas != null)
			{
				Scale = ParentItemsCanvas.scale / ScaleFactor;
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
			zoomableCanvas.ItemRealized += Canvas_ItemRealized;
			zoomableCanvas.ItemVirtualized += Canvas_ItemVirtualized;
			zoomableCanvas.ItemsOwner.ItemsSource = itemsSource.VirtualItemsSource;

			zoomableCanvas.Scale = scale;
			zoomableCanvas.Offset = offset;		
		}


		public void AddItem(IItem item) => itemsSource.Add(item);


		public void AddItems(IEnumerable<IItem> items) => itemsSource.Add(items);

		public void RemoveItem(IItem item) => itemsSource.Remove(item);

		public void UpdateItem(IItem item) => itemsSource.Update(item);


		public double GetZoomScale(int zoomDelta) => Scale * Math.Pow(2, zoomDelta / ZoomSpeed);


		public void Zoom(int zoomDelta, Point viewPosition)
		{
			double zoom = Math.Pow(2, zoomDelta / ZoomSpeed);

			double newScale = Scale * zoom;
			zoom = newScale / Scale;

			Scale = newScale;

			// Adjust the offset to make the point under the mouse stay still.
			Vector position = (Vector)viewPosition;
			Offset = (Point)((Vector)(Offset + position) * zoom - position);
		}

		public void Zoom(double zoom)
		{
			double newScale = Scale * zoom;
			zoom = newScale / Scale;

			Scale = newScale;

			//// Adjust the offset to make the point under the mouse stay still.
			//Vector position = (Vector)viewPosition;
			//Offset = (Point)((Vector)(Offset + position) * zoom - position);
		}


		public void Move(Vector viewOffset)
		{
			Offset -= viewOffset;
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


		public override string ToString() => ownerItem?.ToString() ?? "<root>";

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