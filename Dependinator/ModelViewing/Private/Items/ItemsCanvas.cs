using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using Dependinator.ModelViewing.Nodes;
using Dependinator.ModelViewing.Private.Items.Private;
using Dependinator.Utils;
using Dependinator.Utils.UI;
using Dependinator.Utils.UI.VirtualCanvas;


namespace Dependinator.ModelViewing.Private.Items
{
	internal class ItemsCanvas : Notifyable, IItemsSourceArea
	{
		private static readonly int DefaultScaleFactor = 7;
		private readonly IItemsCanvasBounds owner;
		private readonly ItemsSource itemsSource;
		private readonly ItemsCanvas canvasParent;
		private ZoomableCanvas zoomableCanvas;
		private readonly List<ItemsCanvas> canvasChildren = new List<ItemsCanvas>();

		private Rect ItemsCanvasBounds =>
			owner?.ItemBounds ?? zoomableCanvas?.ActualViewbox ?? Rect.Empty;


		private bool IsShowing => owner?.IsShowing ?? true;

		public ItemsCanvas()
			: this(null, null)
		{
			Scale = 1;
		}

		private ItemsCanvas(IItemsCanvasBounds owner, ItemsCanvas canvasParent)
		{
			this.owner = owner;
			this.canvasParent = canvasParent;
			itemsSource = new ItemsSource(this);
			CanvasRoot = canvasParent?.CanvasRoot ?? this;
		}

		public double ParentScale => IsRoot ? Scale : canvasParent.Scale;

		public ItemsCanvas CanvasRoot { get; }

		public IReadOnlyList<ItemsCanvas> CanvasChildren => canvasChildren;

		public bool IsRoot => canvasParent == null;

		public double ScaleFactor { get; private set; } = 1.0;

		public ItemsCanvas CreateChildCanvas(IItemsCanvasBounds canvasBounds)
		{
			ItemsCanvas child = new ItemsCanvas(canvasBounds, this);
			child.Scale = Scale / DefaultScaleFactor;
			canvasChildren.Add(child);

			return child;
		}

		public bool IsZoomAndMoveEnabled { get; set; } = true;

		public void ResetLayout()
		{
			if (!IsRoot)
			{
				Scale = ParentScale / DefaultScaleFactor;
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


		public Point Offset
		{
			get => Get();
			set
			{
				Set(value);

				if (zoomableCanvas != null)
				{
					zoomableCanvas.Offset = value;
				}
			}
		}


		public double Scale
		{
			get => Get();
			set
			{
				Set(value);

				if (zoomableCanvas != null)
				{
					zoomableCanvas.Scale = value;
				}

				if (canvasParent != null)
				{
					ScaleFactor = canvasParent.Scale / Scale;
				}
			}
		}


		public void Zoom(double zoom, Point? zoomCenter = null)
		{
			if (!IsZoomAndMoveEnabled)
			{
				return;
			}

			double newScale = Scale * zoom;
			if (!IsShowing || IsRoot && newScale < 0.15 && zoom < 1)
			{
				// Item not shown or reached minimum root zoom level
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

			

			UpdateAndNotifyAll();

			ZoomChildren();
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

		private void ZoomChildren()
		{
			canvasChildren.ForEach(child => child.UpdateScale());
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
			double newScale = ParentScale / ScaleFactor;
			double zoom = newScale / Scale;

			if (Math.Abs(zoom) > 0.001)
			{
				Zoom(zoom, new Point(0, 0));
			}
			else
			{
				Log.Warn("Zoom not needed");
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


		public Point RootScreenToCanvasPoint(Point rootScreenPoint)
		{
			if (IsRoot)
			{
				// Adjust for windows title and toolbar bar 
				Point adjustedScreenPoint = rootScreenPoint - new Vector(4, 32);

				return ScreenToCanvasPoint(adjustedScreenPoint);
			}

			Point parentCanvasPoint = canvasParent.RootScreenToCanvasPoint(rootScreenPoint);

			return ParentToChildCanvasPoint(parentCanvasPoint);
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


		//public int AllItemsCount()
		//{
		//	int count = itemsSource.GetAll<IItem>().Count;

		//	count += canvasChildren.Sum(canvas => canvas.AllItemsCount());
		//	return count;
		//}

		//public int ShownItemsCount()
		//{
		//	int count = itemsSource.GetAll<IItem>().Count(i => i.IsShowing);

		//	count += canvasChildren.Sum(canvas => canvas.ShownItemsCount());
		//	return count;
		//}


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


		private Point CanvasToVisualPoint(Point canvasPoint) =>
			(Point)(((Vector)canvasPoint * Scale) - (Vector)Offset);


		private Point ScreenToCanvasPoint(Point screenPoint) =>
			(Point)(((Vector)Offset + (Vector)screenPoint) / Scale);

	}
}