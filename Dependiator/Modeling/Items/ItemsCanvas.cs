using System;
using System.Collections.Generic;
using System.Windows;
using Dependiator.Utils.UI.VirtualCanvas;


namespace Dependiator.Modeling.Items
{
	internal class ItemsCanvas
	{
		private readonly IItem ownerItem;
		private readonly ItemsSource itemsSource;


		private ZoomableCanvas zoomableCanvas;
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
			get => scale;
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


		public void Zoom(double zoom, Point? zoomCenter = null)
		{
			double newScale = Scale * zoom;
			double scaleFactor = newScale / Scale;
			Scale = newScale;

			// Adjust the offset to make the point under the mouse stay still (if provided).
			if (zoomCenter.HasValue)
			{		
				Vector position = (Vector)zoomCenter;
				Offset = (Point)((Vector)(Offset + position) * scaleFactor - position);
			}
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