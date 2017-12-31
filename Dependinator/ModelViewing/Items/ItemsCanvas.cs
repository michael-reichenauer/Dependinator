using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using Dependinator.Common;
using Dependinator.ModelViewing.Items.Private;
using Dependinator.ModelViewing.Nodes;
using Dependinator.Utils;
using Dependinator.Utils.UI.Mvvm;
using Dependinator.Utils.UI.VirtualCanvas;


namespace Dependinator.ModelViewing.Items
{
	internal class ItemsCanvas : Notifyable, IItemsSourceArea
	{
		public static readonly double DefaultScaleFactor = 1.0 / 7.0;
		private readonly IItemsCanvasBounds owner;
		private readonly ItemsSource itemsSource;
		private readonly List<ItemsCanvas> canvasChildren = new List<ItemsCanvas>();

		private ItemsCanvas parentCanvas;
		private ZoomableCanvas zoomableCanvas;

		public ZoomableCanvas ZoomableCanvas => zoomableCanvas;

		private Rect ItemsCanvasBounds =>
			owner?.ItemBounds ?? zoomableCanvas?.ActualViewbox ?? Rect.Empty;
		private bool isFocused;


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
				isFocused = true;
			}
			else
			{
				// Creating child node canvas
				ScaleFactor = DefaultScaleFactor;

				parentCanvas.canvasChildren.Add(this);
			}
		}


		public bool IsFocused
		{
			get => isFocused;
			set
			{
				isFocused = value;

				// Root canvas is focused if no other canvas is focused
				RootCanvas.isFocused = isFocused ? IsRoot : !IsRoot;
			}
		}

		public bool IsZoomAndMoveEnabled { get; set; } = true;

		public ItemsCanvas RootCanvas { get; }
		public bool IsRoot => parentCanvas == null;
		private double rootScale;

		public IReadOnlyList<ItemsCanvas> CanvasChildren => canvasChildren;


		public double ScaleFactor { get; private set; }


		public double Scale => parentCanvas?.Scale * ScaleFactor ?? rootScale;



		public void OnMouseWheel(UIElement uiElement, MouseWheelEventArgs e, bool isSelected)
		{
			int wheelDelta = e.Delta;
			double zoom = Math.Pow(2, wheelDelta / 2000.0);

			if (isSelected)
			{
				Point viewPosition = e.GetPosition(uiElement);
				ZoomNode(zoom, viewPosition);
			}
			else
			{
				Point viewPosition = e.GetPosition(RootCanvas.ZoomableCanvas);
				ZoomRootNode(zoom, viewPosition);
			}
			
			e.Handled = true;
		}


		public void SetRootScale(double scale) => rootScale = scale;

		public void SetScaleFactor(double scaleFactor) => ScaleFactor = scaleFactor;


		public void RemoveChildCanvas(ItemsCanvas childCanvas)
		{
			childCanvas.parentCanvas = null;
			canvasChildren.Remove(childCanvas);
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


		public void UpdateAll()
		{
			ZoomRootNode(1, new Point(0, 0));
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
			if (!IsShowing || !CanShow || IsRoot && newScale < 0.40 && zoom < 1)
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


		public void MoveRootCanvas(Vector viewOffset)
		{
			RootCanvas.MoveCanvas(viewOffset);
		}


		public bool IsNodeInViewBox(Rect bounds)
		{
			Rect viewbox = GetViewBox();

			return viewbox.IntersectsWith(bounds);
		}


		public void MoveCanvas(Vector viewOffset)
		{
			if (!IsZoomAndMoveEnabled)
			{
				return;
			}

			Vector moveOffset = new Vector(viewOffset.X / Scale, viewOffset.Y / Scale);

			Rect viewbox = GetViewBox();

			if (!IsAnyNodesWithinView(viewbox, moveOffset))
			{
				// No node (if moved) would be withing visibale view
				return;
			}
			
			itemsSource.GetAllItems().ForEach(item => item.MoveItem(moveOffset));

			UpdateAndNotifyAll();
			TriggerInvalidated();
			UpdateShownItemsInChildren();
		}


		private Rect GetViewBox()
		{
			Rect viewbox = zoomableCanvas.ActualViewbox;
			viewbox.Inflate(-30 / Scale, -30 / Scale);
			return viewbox;
		}


		private bool IsAnyNodesWithinView(Rect viewbox, Vector moveOffset)
		{
			IEnumerable<IItem> nodes = itemsSource.GetAllItems().Where(i => i is NodeViewModel );
			foreach (IItem node in nodes)
			{
				if (node.CanShow)
				{
					if (IsNodeInViewBox(node, viewbox, moveOffset))
					{
						return true;
					}
				}
			}

			return false;
		}


		private static bool IsNodeInViewBox(IItem node, Rect viewbox, Vector moveOffset)
		{
			Point location = node.ItemBounds.Location;
			Size size = node.ItemBounds.Size;

			Rect newBounds = new Rect(location + moveOffset, size);
			if (viewbox.IntersectsWith(newBounds))
			{
				return true;
			}

			return false;
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



		public void UpdateScale()
		{
			if (!IsShowing || !CanShow)
			{
				// Item not shown or will be hidden
				return;
			}

			SetZoomableCanvasScale(null);

			UpdateAndNotifyAll();

			canvasChildren.ForEach(child => child.UpdateScale());
		}


		private void SetZoomableCanvasScale(Point? zoomCenter)
		{
			if (zoomableCanvas != null)
			{
				if (zoomCenter.HasValue)
				{
					// Adjust the offset to make the point at the center of zoom area stay still
					double zoomFactor = Scale / zoomableCanvas.Scale;
					Vector position = (Vector)zoomCenter;

					Vector moveOffset = position * zoomFactor - position;
					MoveCanvas(-moveOffset);
				}

				zoomableCanvas.Scale = Scale;
			}
		}


		public void Test()
		{
			//if (ParentItemsCanvas != null)
			//{
			//	// Get the position of the nodes items view relative the nodes border
			//	UIElement innerBorder = VisualTreeHelper.GetParent(view) as UIElement;
			//	UIElement grid = VisualTreeHelper.GetParent(innerBorder) as UIElement;
			//	UIElement nodesBorder = VisualTreeHelper.GetParent(grid) as UIElement;

			//	relative = (Vector)view.TranslatePoint(new Point(0, 0), nodesBorder);
			//}

			UIElement innerBorder = VisualTreeHelper.GetParent(zoomableCanvas) as UIElement;
			UIElement grid = VisualTreeHelper.GetParent(innerBorder) as UIElement;
			UIElement nodesBorder = VisualTreeHelper.GetParent(grid) as UIElement;

			//	relative = (Vector)view.TranslatePoint(new Point(0, 0), nodesBorder);

		}

		public Point ChildToParentCanvasPoint(Point childCanvasPoint)
		{
			if (!IsRoot)
			{
				// Point within the parent node
				Vector parentPoint = (Vector)childCanvasPoint * ScaleFactor;

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


		public Point CanvasPointToScreenPoint(Point canvasPoint)
		{
			if (IsRoot)
			{
				// Adjust for windows title and toolbar bar 
				Point adjustedScreenPoint = canvasPoint + new Vector(4, 32);

				return adjustedScreenPoint;
			}

			Point parentCanvasPoint = parentCanvas.ChildToParentCanvasPoint(canvasPoint);
			Point screenPoint = parentCanvas.CanvasPointToScreenPoint(parentCanvasPoint);

			return screenPoint;
		}


		public Point CanvasPointToScreenPoint2(Point canvasPoint)
		{
			//UIElement itemsPresenter = VisualTreeHelper.GetParent(zoomableCanvas) as UIElement;
			//UIElement scroll = VisualTreeHelper.GetParent(itemsPresenter) as UIElement;
			//UIElement grid = VisualTreeHelper.GetParent(scroll) as UIElement;
			//UIElement scrollView = VisualTreeHelper.GetParent(grid) as UIElement;
			//UIElement border = VisualTreeHelper.GetParent(scrollView) as UIElement;
			//UIElement ListBox = VisualTreeHelper.GetParent(border) as UIElement;
			//UIElement content = VisualTreeHelper.GetParent(ListBox) as UIElement;
			//UIElement border2 = VisualTreeHelper.GetParent(content) as UIElement;
			//UIElement itemsView = VisualTreeHelper.GetParent(border2) as UIElement;


			return zoomableCanvas.TranslatePoint(canvasPoint, RootCanvas.zoomableCanvas);
		}


		public Point ParentToChildCanvasPoint(Point parentCanvasPoint)
		{
			if (!IsRoot)
			{
				Point relativeParentPoint = parentCanvasPoint - (Vector)ItemsCanvasBounds.Location;

				// Point within the parent node
				Point parentChildPoint = (Point)((Vector)relativeParentPoint / ScaleFactor);

				Point compensatedPoint = parentChildPoint;
				Point childPoint = compensatedPoint;

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
				0 / scale, 0 / scale, renderSize.Width / scale, renderSize.Height / scale);

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
			(Point)(((Vector)canvasPoint * Scale));


		private Point ScreenToCanvasPoint(Point screenPoint) =>
			(Point)(((Vector)screenPoint) / Scale);
	}
}