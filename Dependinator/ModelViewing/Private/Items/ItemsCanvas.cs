using System.Collections.Generic;
using System.Linq;
using System.Windows;
using Dependinator.ModelViewing.Private.Items.Private;
using Dependinator.Utils.UI.Mvvm;
using Dependinator.Utils.UI.VirtualCanvas;


namespace Dependinator.ModelViewing.Private.Items
{
	internal class ItemsCanvas : Notifyable, IItemsSourceArea
	{
		private static readonly double DefaultScaleFactor = 1.0 / 7.0;
		private readonly IItemsCanvasBounds owner;
		private readonly ItemsSource itemsSource;
		private readonly List<ItemsCanvas> canvasChildren = new List<ItemsCanvas>();

		private ItemsCanvas parentCanvas;
		private ZoomableCanvas zoomableCanvas;


		private Rect ItemsCanvasBounds =>
			owner?.ItemBounds ?? zoomableCanvas?.ActualViewbox ?? Rect.Empty;


		private bool IsShowing => owner?.IsShowing ?? true;
		private bool CanShow => owner?.CanShow ?? true;


		// The root canvas
		public ItemsCanvas()
			: this(null, null)
		{
		}

		public ItemsCanvas(IItemsCanvasBounds owner, ItemsCanvas parentCanvas)
		{
			this.owner = owner;
			itemsSource = new ItemsSource(this);
			this.parentCanvas = parentCanvas;
			RootCanvas = parentCanvas?.RootCanvas ?? this;

			if (parentCanvas == null)
			{
				// Creating root node canvas
				rootScale = 1;
				ScaleFactor = 1;
			}
			else
			{
				// Creating child node canvas
				ScaleFactor = DefaultScaleFactor;

				parentCanvas.canvasChildren.Add(this);
			}
		}

		public bool IsZoomAndMoveEnabled { get; set; } = true;

		public ItemsCanvas RootCanvas { get; }
		public bool IsRoot => parentCanvas == null;
		private double rootScale;

		public IReadOnlyList<ItemsCanvas> CanvasChildren => canvasChildren;


		public double ScaleFactor { get; set; }


		public double Scale => parentCanvas?.Scale * ScaleFactor ?? rootScale;


		public Point Offset
		{
			get => Get();
			private set
			{
				Set(value);

				if (zoomableCanvas != null)
				{
					zoomableCanvas.Offset = value;
				}
			}
		}


		public void SetOffset(Point offsetPoint)
		{
			Offset = offsetPoint;
		}



		public void SetRootScale(double scale) => rootScale = scale;

		public void SetScaleFactor(double scaleFactor) => ScaleFactor = scaleFactor;


		public void RemoveChildCanvas(ItemsCanvas childCanvas)
		{
			childCanvas.parentCanvas = null;
			canvasChildren.Remove(childCanvas);
		}



		public void ResetLayout()
		{
			if (IsRoot)
			{
				ScaleFactor = 1;
				Offset = new Point(0, 0);
			}
			else
			{
				ScaleFactor = DefaultScaleFactor;
				Offset = new Point(0, 0);
			}
		}

		public void ItemRealized()
		{
			itemsSource.ItemRealized();
			UpdateScale();
		}


		public void ItemVirtualized()
		{
			itemsSource.ItemVirtualized();
		}


		public void ZoomRootNode(double zoom, Point zoomCenter)
		{
			RootCanvas.ZoomNode(zoom, zoomCenter);
		}


		public void ZoomNode(double zoom, Point zoomCenter)
		{
			if (!IsZoomAndMoveEnabled)
			{
				return;
			}

			double oldScale = Scale;
			double newScale = oldScale * zoom;
			if (!IsShowing || !CanShow || IsRoot && newScale < 0.20 && zoom < 1)
			{
				// Item not shown or reached minimum root zoom level
				return;
			}



			if (IsRoot)
			{
				rootScale = newScale;
			}
			else
			{
				ScaleFactor = newScale / parentCanvas.Scale;
			}

			SetZoomableCanvasScale(zoomCenter);

			UpdateAndNotifyAll();

			canvasChildren.ForEach(child => child.UpdateScale());
		}



		public void UpdateAndNotifyAll()
		{
			itemsSource.UpdateAndNotifyAll();
		}


		public void Move(Vector viewOffset)
		{
			if (!IsZoomAndMoveEnabled)
			{
				return;
			}

			Offset -= viewOffset;
			UpdateShownItemsInChildren();
		}


		private void UpdateShownItemsInChildren()
		{
			canvasChildren
				.Where(canvas => canvas.IsShowing)
				.ForEach(canvas =>
				{
					canvas.TriggerInvalidated();
					canvas.UpdateShownItemsInChildren();
				});
		}



		private void UpdateScale()
		{
			if (!IsShowing || !CanShow)
			{
				// Item not shown or will be hidden
				return;
			}

			SetZoomableCanvasScale(new Point(0, 0));

			UpdateAndNotifyAll();

			canvasChildren.ForEach(child => child.UpdateScale());
		}


		private void SetZoomableCanvasScale(Point zoomCenter)
		{
			if (zoomableCanvas != null)
			{
				double zoomFactor = Scale / zoomableCanvas.Scale; 

				// Adjust the offset to make the point at the center of zoom area stay still
				Vector position = (Vector)zoomCenter;
				Offset = (Point)((Vector)(Offset + position) * zoomFactor - position);

				zoomableCanvas.Scale = Scale;
			}
		}


		public Point ChildToParentCanvasPoint(Point childCanvasPoint)
		{
			if (!IsRoot)
			{
				Vector vector = (Vector)Offset / Scale;
				Point childPoint = childCanvasPoint - vector;

				// Point within the parent node
				Vector parentPoint = (Vector)childPoint * ScaleFactor;

				// point in parent canvas scale
				Point childToParentCanvasPoint = ItemsCanvasBounds.Location + parentPoint;
				return childToParentCanvasPoint;
			}
			else
			{
				return childCanvasPoint;
			}
		}


		public Point RootScreenToCanvasPoint(Point rootScreenPoint)
		{
			if (IsRoot)
			{
				// Adjust for windows title and toolbar bar 
				Point adjustedScreenPoint = rootScreenPoint - new Vector(4, 32);

				return ScreenToCanvasPoint(adjustedScreenPoint);
			}

			Point parentCanvasPoint = parentCanvas.RootScreenToCanvasPoint(rootScreenPoint);

			return ParentToChildCanvasPoint(parentCanvasPoint);
		}



		public Point ParentToChildCanvasPoint(Point parentCanvasPoint)
		{
			if (!IsRoot)
			{
				Point relativeParentPoint = parentCanvasPoint - (Vector)ItemsCanvasBounds.Location;

				// Point within the parent node
				Point parentChildPoint = (Point)((Vector)relativeParentPoint / ScaleFactor);

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
			zoomableCanvas.ItemsOwner.ItemsSource = itemsSource;

			zoomableCanvas.Scale = Scale;
			zoomableCanvas.Offset = Offset;
		}


		public void AddItem(ItemViewModel item)
		{
			item.ItemOwnerCanvas = this;
			itemsSource.Add(item);
		}

		public void RemoveAll()
		{
			itemsSource.RemoveAll();
			canvasChildren.Clear();
		}

		public void RemoveItem(ItemViewModel item) => itemsSource.Remove(item);

		public void UpdateItem(ItemViewModel item) => itemsSource.Update(item);


		public void SizeChanged() => itemsSource.TriggerExtentChanged();

		public void TriggerInvalidated() => itemsSource.TriggerInvalidated();


		public Rect GetHierarchicalVisualArea()
		{
			if (!IsRoot)
			{
				Rect parentArea = parentCanvas.GetHierarchicalVisualArea();
				Point p1 = ParentToChildCanvasPoint(parentArea.Location);
				Size s1 = (Size)((Vector)parentArea.Size / ScaleFactor);
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

		public int ChildItemsCount()
		{
			return itemsSource.GetAllItems().Count();
		}

		public int ShownChildItemsCount()
		{
			return itemsSource.GetAllItems().Count(item => item.IsShowing);
		}


		public int DescendantsItemsCount()
		{
			int count = itemsSource.GetAllItems().Count();

			count += canvasChildren.Sum(canvas => canvas.DescendantsItemsCount());
			return count;
		}

		public int ShownDescendantsItemsCount()
		{
			int count = itemsSource.GetAllItems().Count(item => item.IsShowing);

			count += canvasChildren.Sum(canvas => canvas.ShownDescendantsItemsCount());
			return count;
		}


		private Rect GetItemsCanvasViewArea()
		{
			double scale = Scale;
			double parentScale = parentCanvas?.Scale ?? scale;

			Size renderSize = (Size)((Vector)ItemsCanvasBounds.Size * parentScale);

			Rect value = new Rect(
				Offset.X / scale, Offset.Y / scale, renderSize.Width / scale, renderSize.Height / scale);

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


		private Point CanvasToVisualPoint(Point canvasPoint) =>
			(Point)(((Vector)canvasPoint * Scale) - (Vector)Offset);


		private Point ScreenToCanvasPoint(Point screenPoint) =>
			(Point)(((Vector)Offset + (Vector)screenPoint) / Scale);
	}
}