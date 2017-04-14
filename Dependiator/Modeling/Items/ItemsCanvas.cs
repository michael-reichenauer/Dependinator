using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Media;
using Dependiator.Utils.UI.VirtualCanvas;


namespace Dependiator.Modeling.Items
{
	internal class ItemsCanvas
	{
		private readonly IItemBounds itemBounds;

		private readonly ItemsSource itemsSource;
		private NodesView view;
		private Vector relative = new Vector(0, 0);

		private ZoomableCanvas zoomableCanvas;
		private double scale = 1.0;
		private Point offset = new Point(0, 0);


		public ItemsCanvas(IItemBounds itemBounds, ItemsCanvas parentItemsCanvas)
		{
			this.itemBounds = itemBounds;
			ParentItemsCanvas = parentItemsCanvas;
			itemsSource = new ItemsSource(this);
		}


		public Rect LastViewAreaQuery => itemsSource.LastViewAreaQuery;
		public Rect ItemBounds => itemBounds?.NodeBounds ?? zoomableCanvas?.ActualViewbox ?? Rect.Empty;
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
				double newScale = ParentItemsCanvas.Scale / ScaleFactor;
				double zoom = newScale / Scale;

				Zoom(zoom, new Point(0, 0));
			}			
		}


		public Point GetChildToParentCanvasPoint(Point childCanvasPoint)
		{
			if (ParentItemsCanvas != null && zoomableCanvas != null)
			{
				// Compensate the canvas view for the nodes vew position relative nodes border 
				Point compensatedPoint = childCanvasPoint - relative;

				Vector vector = (Vector)Offset / Scale;

				Point childPoint = compensatedPoint - vector;

				// Point within the parent node
				//Point parentPoint = new Point(childPoint.X / ScaleFactor, childPoint.Y / ScaleFactor);
				Vector parentPoint = (Vector)childPoint / ScaleFactor;

				// point in parent canvas scale
				//Point childToParentCanvasPoint = new Point(ItemBounds.X + parentPoint.X, ItemBounds.Y + parentPoint.Y);
				Point childToParentCanvasPoint = ItemBounds.Location + parentPoint;
				return childToParentCanvasPoint;
			}
			else
			{
				return childCanvasPoint;
			}
		}

		public Point GetParentToChildCanvasPoint(Point parentCanvasPoint)
		{
			if (ParentItemsCanvas != null && zoomableCanvas != null)
			{
				Point relativeParentPoint = parentCanvasPoint - (Vector)ItemBounds.Location;

				// Point within the parent node
				//Point parentChildPoint = new Point(relativeParentPoint.X * ScaleFactor, relativeParentPoint.Y * ScaleFactor);
				Point parentChildPoint = (Point)((Vector)relativeParentPoint * ScaleFactor);

				Point compensatedPoint = parentChildPoint + relative;

				Vector vector = (Vector)Offset / Scale;

				Point childPoint = compensatedPoint + vector;

				return childPoint;
			}
			else
			{
				return parentCanvasPoint;
			}
		}


		public void SetCanvas(ZoomableCanvas canvas, NodesView nodesView)
		{
			view = nodesView;

			if (ParentItemsCanvas != null)
			{
				// Get the position of the nodes items view relative the nodes border
				UIElement innerBorder = VisualTreeHelper.GetParent(view) as UIElement;
				UIElement grid = VisualTreeHelper.GetParent(innerBorder) as UIElement;
				UIElement nodesBorder = VisualTreeHelper.GetParent(grid) as UIElement;

				relative = (Vector)view.TranslatePoint(new Point(0, 0), nodesBorder);
			}

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

		public void UpdateItems(IEnumerable<IItem> items) => itemsSource.Update(items);


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

		public override string ToString() => itemBounds?.ToString() ?? "<root>";

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