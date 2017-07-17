using System.Collections.Generic;
using System.Linq;
using System.Windows;
using Dependinator.Modeling;
using Dependinator.Utils.UI.VirtualCanvas;


namespace Dependinator.ModelViewing.Private.Items.Private
{
	internal class ItemsCanvas : IItemsCanvas, IItemsSourceArea
	{
		private readonly IItemsCanvasBounds owner;
		private readonly ItemsSource itemsSource;
		private readonly IItemsCanvas canvasParent;
		private ZoomableCanvas zoomableCanvas;
		private readonly List<ItemsCanvas> canvasChildren = new List<ItemsCanvas>();
		private double scale = 1.0;
		private Point offset = new Point(0, 0);
		private Rect ItemsCanvasBounds => 
			owner?.ItemBounds ?? zoomableCanvas?.ActualViewbox ?? Rect.Empty;

		private bool IsShowing => owner?.IsShowing ?? true;

		public ItemsCanvas()
			: this(null, null)
		{
			
		}

		private ItemsCanvas(IItemsCanvasBounds owner, IItemsCanvas canvasParent)
		{
			this.owner = owner;
			this.canvasParent = canvasParent;
			itemsSource = new ItemsSource(this);
			CanvasRoot = canvasParent?.CanvasRoot ?? this;
		}

		public IItemsCanvas CanvasRoot { get; }

		public IReadOnlyList<IItemsCanvas> CanvasChildren => canvasChildren;

		public bool IsRoot => canvasParent == null;

		public double ScaleFactor { get; private set; } = 1.0;

		public IItemsCanvas CreateChild(IItemsCanvasBounds canvasBounds)
		{
			ItemsCanvas child = new ItemsCanvas(canvasBounds, this);
			canvasChildren.Add(child);

			return child;
		}

		public void SetInitialScale(double initialScale) => Scale = initialScale;

		public Point Offset
		{
			get => offset;
			private set
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
			private set
			{
				scale = value;

				if (zoomableCanvas != null)
				{
					zoomableCanvas.Scale = value;
				}

				if (canvasParent != null)
				{
					ScaleFactor = canvasParent.Scale / scale;
				}
			}
		}


		public void Zoom(double zoom, Point? zoomCenter = null)
		{
			double newScale = Scale * zoom;
			if (!IsShowing || IsRoot && newScale < 0.15 && zoom < 1)
			{
				return;
			}

			

			double scaleFactor = newScale / Scale;
			Scale = newScale;

			// Adjust the offset to make the point at the center of zoom area stay still (if provided)
			if (zoomCenter.HasValue)
			{
				Vector position = (Vector)zoomCenter;
				Offset = (Point)((Vector)(Offset + position) * scaleFactor - position);
			}

			var items = itemsSource.GetAll<ItemViewModel>();
			itemsSource.Update(items);
			items.ForEach(item => item.NotifyAll());

			canvasChildren.ForEach(child => child.UpdateScale());
		}


		public void Move(Vector viewOffset) => Offset -= viewOffset;


		public double ParentScale => IsRoot ? Scale : canvasParent.Scale;


		private void UpdateScale()
		{
			double newScale = canvasParent.Scale / ScaleFactor;
			double zoom = newScale / Scale;

			Zoom(zoom, new Point(0, 0));			
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


		public void AddItem(ItemViewModel item)
		{
			item.ItemsCanvas = this;
			itemsSource.Add(item);
		}


		public void AddItems(IEnumerable<ItemViewModel> items)
		{
			items.ForEach(item => item.ItemsCanvas = this);
			itemsSource.Add(items);
		}

		public void RemoveItem(ItemViewModel item) => itemsSource.Remove(item);

		public void RemoveAll() => itemsSource.RemoveAll();


		public void UpdateItem(ItemViewModel item) => itemsSource.Update(item);

		public void UpdateItems(IEnumerable<ItemViewModel> items) => itemsSource.Update(items);



		public void SizeChanged() => itemsSource.TriggerExtentChanged();

		public void TriggerInvalidated() => itemsSource.TriggerInvalidated();


		public Rect GetHierarchicalVisualArea()
		{
			if (!IsRoot)
			{
				Rect parentArea = canvasParent.GetHierarchicalVisualArea();
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


		public override string ToString() => owner?.ToString() ?? NodeName.Root.ToString();


		private Rect GetItemsCanvasViewArea()
		{
			double parentScale = canvasParent?.Scale ?? Scale;

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