using System.Collections.Generic;
using System.Windows;
using Dependinator.Utils.UI.VirtualCanvas;


namespace Dependinator.ModelViewing.Private.Items.Private
{
	internal class ItemsCanvas : IItemsCanvas, IItemsSourceArea
	{
		private readonly IItemsCanvasBounds itemsCanvasBounds;
		private readonly ItemsSource itemsSource;
		private readonly IItemsCanvas parentItemsCanvas;
		private ZoomableCanvas zoomableCanvas;
		private double scale = 1.0;
		private Point offset = new Point(0, 0);
		private Rect ItemsCanvasBounds => 
			itemsCanvasBounds?.NodeBounds ?? zoomableCanvas?.ActualViewbox ?? Rect.Empty;


		public ItemsCanvas(IItemsCanvasBounds itemsCanvasBounds, IItemsCanvas parentItemsCanvas)
		{
			this.itemsCanvasBounds = itemsCanvasBounds;
			this.parentItemsCanvas = parentItemsCanvas;
			itemsSource = new ItemsSource(this);
		}

		public bool IsRoot => parentItemsCanvas == null;

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

				if (parentItemsCanvas != null)
				{
					ScaleFactor = parentItemsCanvas.Scale / scale;
				}
			}
		}


		public void Zoom(double zoom, Point? zoomCenter = null)
		{
			double newScale = Scale * zoom;
			double scaleFactor = newScale / Scale;
			Scale = newScale;

			// Adjust the offset to make the point at the center of zoom area stay still (if provided)
			if (zoomCenter.HasValue)
			{
				Vector position = (Vector)zoomCenter;
				Offset = (Point)((Vector)(Offset + position) * scaleFactor - position);
			}
		}


		public void Move(Vector viewOffset) => Offset -= viewOffset;


		public void UpdateScale()
		{
			if (!IsRoot)
			{
				double newScale = parentItemsCanvas.Scale / ScaleFactor;
				double zoom = newScale / Scale;

				Zoom(zoom, new Point(0, 0));
			}
		}


		public Point ChildToParentCanvasPoint(Point childCanvasPoint)
		{
			if (!IsRoot)
			{
				Vector vector = (Vector)Offset / Scale;
				Point childPoint = childCanvasPoint - vector;

				// Point within the parent node
				Vector parentPoint = (Vector)childPoint / ScaleFactor;

				// point in parent canvas scale
				Point childToParentCanvasPoint = ItemsCanvasBounds.Location + parentPoint;
				return childToParentCanvasPoint;
			}
			else
			{
				return childCanvasPoint;
			}
		}


		public Point ParentToChildCanvasPoint(Point parentCanvasPoint)
		{
			if (!IsRoot)
			{
				Point relativeParentPoint = parentCanvasPoint - (Vector)ItemsCanvasBounds.Location;

				// Point within the parent node
				Point parentChildPoint = (Point)((Vector)relativeParentPoint * ScaleFactor);

				Point compensatedPoint = parentChildPoint;

				Vector vector = (Vector)Offset / Scale;

				Point childPoint = compensatedPoint + vector;

				return childPoint;
			}
			else
			{
				return parentCanvasPoint;
			}
		}


		public void SetZoomableCanvas(ZoomableCanvas canvas)
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

		public void RemoveAll() => itemsSource.RemoveAll();


		public void UpdateItem(IItem item) => itemsSource.Update(item);

		public void UpdateItems(IEnumerable<IItem> items) => itemsSource.Update(items);



		public void SizeChanged() => itemsSource.TriggerExtentChanged();

		public void TriggerInvalidated() => itemsSource.TriggerInvalidated();


		public Rect GetHierarchicalVisualArea()
		{
			if (!IsRoot)
			{
				Rect parentArea = parentItemsCanvas.GetHierarchicalVisualArea();
				Point p1 = ParentToChildCanvasPoint(parentArea.Location);
				Size s1 = (Size)((Vector)parentArea.Size * ScaleFactor);
				Rect scaledParentViewArea = new Rect(p1, s1);
				Rect viewArea = GetItemsCanvasViewArea();
				viewArea.Intersect(scaledParentViewArea);
				return viewArea;
			}
			else
			{
				return GetItemsCanvasViewArea();
			}
		}


		public override string ToString() => itemsCanvasBounds?.ToString() ?? "<root>";


		private Rect GetItemsCanvasViewArea()
		{
			double parentScale = parentItemsCanvas?.Scale ?? Scale;

			Size renderSize = (Size)((Vector)ItemsCanvasBounds.Size * parentScale);

			Rect value = new Rect(
				Offset.X / Scale, Offset.Y / Scale, renderSize.Width / Scale, renderSize.Height / Scale);

			return value;
		}


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