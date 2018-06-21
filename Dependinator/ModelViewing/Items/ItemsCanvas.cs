using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Input;
using Dependinator.ModelViewing.Items.Private;
using Dependinator.ModelViewing.ModelHandling.Core;
using Dependinator.ModelViewing.Nodes;
using Dependinator.Utils;
using Dependinator.Utils.UI.Mvvm;
using Dependinator.Utils.UI.VirtualCanvas;


namespace Dependinator.ModelViewing.Items
{
	internal class ItemsCanvas : Notifyable
	{
		public static readonly double DefaultScaleFactor = 1.0 / 7.0;

		private readonly IItemsCanvasOwner owner;
		private readonly ItemsSource itemsSource;
		private readonly List<ItemsCanvas> canvasChildren = new List<ItemsCanvas>();

		private double rootScale;
		private bool isFocused;
		private bool IsShowing => owner?.IsShowing ?? true;
		private bool CanShow => owner?.CanShow ?? true;


		// The root canvas
		public ItemsCanvas()
			: this(null, null)
		{
		}


		public ItemsCanvas(IItemsCanvasOwner owner, ItemsCanvas parentCanvas)
		{
			this.owner = owner;
			this.ParentCanvas = parentCanvas;

			RootCanvas = parentCanvas?.RootCanvas ?? this;

			VisualAreaHandler visualAreaHandler = new VisualAreaHandler(this);
			itemsSource = new ItemsSource(visualAreaHandler);

			if (IsRoot)
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

				parentCanvas?.canvasChildren.Add(this);
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


		public ZoomableCanvas ZoomableCanvas { get; private set; }

		public ItemsCanvas ParentCanvas { get; private set; }

		public Rect ItemsCanvasBounds =>
			owner?.ItemBounds ?? ZoomableCanvas?.ActualViewbox ?? Rect.Empty;

		public bool IsZoomAndMoveEnabled { get; set; } = true;

		public ItemsCanvas RootCanvas { get; }
		public bool IsRoot => ParentCanvas == null;

		public double ScaleFactor { get; set; }

		public double Scale => ParentCanvas?.Scale * ScaleFactor ?? rootScale;


		public void SetRootScale(double scale) => RootCanvas.rootScale = scale;


		public void AddItem(ItemViewModel item)
		{
			item.ItemOwnerCanvas = this;
			itemsSource.Add(item);
		}


		public void RemoveItem(ItemViewModel item) => itemsSource.Remove(item);

		public void UpdateItem(ItemViewModel item) => itemsSource.Update(item);


		public void SizeChanged() => itemsSource.TriggerExtentChanged();


		public void ZoomNode(MouseWheelEventArgs e)
		{
			int wheelDelta = e.Delta;
			double zoom = Math.Pow(2, wheelDelta / 2000.0);

			Point viewPosition = e.GetPosition(ZoomableCanvas);
			ZoomNode(zoom, viewPosition);

			e.Handled = true;
		}


		public void RemoveChildCanvas(ItemsCanvas childCanvas)
		{
			childCanvas.ParentCanvas = null;
			canvasChildren.Remove(childCanvas);
		}


		public void CanvasRealized() => UpdateScale();

		public void CanvasVirtualized() { }

		public void UpdateAll() => RootCanvas.ZoomNode(1, new Point(0, 0));


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
				ScaleFactor = newScale / ParentCanvas.Scale;
			}

			SetZoomableCanvasScale(zoomCenter);

			UpdateAndNotifyAll();

			canvasChildren.ForEach(child => child.UpdateScale());
		}



		public void UpdateAndNotifyAll()
		{
			IReadOnlyList<ItemViewModel> items = itemsSource.GetAllItems().Cast<ItemViewModel>().ToList();
			itemsSource.Update(items);
			items.ForEach(item => item.NotifyAll());
		}


		public bool IsNodeInViewBox(Rect bounds)
		{
			Rect viewBox = GetViewBox();

			return viewBox.IntersectsWith(bounds);
		}


		public void MoveAllItems(Point sp1, Point sp2)
		{
			if (!IsZoomAndMoveEnabled)
			{
				return;
			}

			Point cp1 = ScreenToCanvasPoint(sp1);
			Point cp2 = ScreenToCanvasPoint(sp2);

			Vector moveOffset = cp2 - cp1;

			MoveCanvasItems(moveOffset);
		}


		public void MoveAllItems(Vector viewOffset)
		{
			if (!IsZoomAndMoveEnabled)
			{
				return;
			}

			Vector moveOffset = new Vector(viewOffset.X / Scale, viewOffset.Y / Scale);


			MoveCanvasItems(moveOffset);
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


		public Point MouseToCanvasPoint()
		{
			Point screenPoint = Mouse.GetPosition(ZoomableCanvas);

			return ZoomableCanvas.GetCanvasPoint(screenPoint);
		}


		public Point MouseEventToCanvasPoint(MouseEventArgs e)
		{
			Point screenPoint = e.GetPosition(ZoomableCanvas);

			return ZoomableCanvas.GetCanvasPoint(screenPoint);
		}



		public Point CanvasToScreenPoint(Point canvasPoint)
		{
			try
			{
				Point localScreenPoint = ZoomableCanvas.GetVisualPoint(canvasPoint);

				Point screenPoint = ZoomableCanvas.PointToScreen(localScreenPoint);

				return screenPoint;
			}
			catch (Exception e)
			{
				Log.Exception(e, $"Node {this}");
				throw;
			}
		}


		public Point ScreenToCanvasPoint(Point screenPoint)
		{
			try
			{
				Point localScreenPoint = ZoomableCanvas.PointFromScreen(screenPoint);

				Point canvasPoint = ZoomableCanvas.GetCanvasPoint(localScreenPoint);

				return canvasPoint;
			}
			catch (Exception e)
			{
				Log.Exception(e, $"Node {this}");
				throw;
			}
		}



		public Point ParentToChildCanvasPoint(Point parentCanvasPoint)
		{
			if (!IsRoot)
			{
				Point relativeParentPoint = parentCanvasPoint - (Vector)ItemsCanvasBounds.Location;

				// Point within the parent node
				Point compensatedPoint = (Point)((Vector)relativeParentPoint / ScaleFactor);

				return compensatedPoint;
			}
			else
			{
				return parentCanvasPoint;
			}
		}


		public Point ParentToChildCanvasPoint2(Point parentCanvasPoint)
		{
			Point parentScreenPoint = ParentCanvas.CanvasToScreenPoint(parentCanvasPoint);
			return ScreenToCanvasPoint(parentScreenPoint);
		}



		public void SetZoomableCanvas(ZoomableCanvas canvas)
		{
			if (ZoomableCanvas != null)
			{
				// New canvas replacing previous canvas
				ZoomableCanvas.ItemRealized -= Canvas_ItemRealized;
				ZoomableCanvas.ItemVirtualized -= Canvas_ItemVirtualized;
			}

			ZoomableCanvas = canvas;
			ZoomableCanvas.IsDisableOffsetChange = true;
			ZoomableCanvas.ItemRealized += Canvas_ItemRealized;
			ZoomableCanvas.ItemVirtualized += Canvas_ItemVirtualized;
			ZoomableCanvas.ItemsOwner.ItemsSource = itemsSource;

			ZoomableCanvas.Scale = Scale;
		}


		public override string ToString() => owner?.ToString() ?? NodeName.Root.ToString();


		public int ChildItemsCount() => itemsSource.GetAllItems().Count();


		public int ShownChildItemsCount() => itemsSource.GetAllItems().Count(item => item.IsShowing);


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


		private void TriggerInvalidated() => itemsSource.TriggerInvalidated();


		private void SetZoomableCanvasScale(Point? zoomCenter)
		{
			if (ZoomableCanvas != null)
			{
				if (zoomCenter.HasValue)
				{
					// Adjust the offset to make the point at the center of zoom area stay still
					double zoomFactor = Scale / ZoomableCanvas.Scale;
					Vector position = (Vector)zoomCenter;

					Vector moveOffset = position * zoomFactor - position;
					MoveAllItems(-moveOffset);
				}

				ZoomableCanvas.Scale = Scale;
			}
		}


		private void MoveCanvasItems(Vector moveOffset)
		{
			Rect viewBox = GetViewBox();

			if (!IsAnyNodesWithinView(viewBox, moveOffset))
			{
				// No node (if moved) would be withing visible view
				return;
			}

			itemsSource.GetAllItems().ForEach(item => item.MoveItem(moveOffset));

			UpdateAndNotifyAll();
			TriggerInvalidated();
			UpdateShownItemsInChildren();
		}


		private Rect GetViewBox()
		{
			Rect viewBox = ZoomableCanvas.ActualViewbox;
			viewBox.Inflate(-30 / Scale, -30 / Scale);
			return viewBox;
		}


		private bool IsAnyNodesWithinView(Rect viewBox, Vector moveOffset)
		{
			IEnumerable<IItem> nodes = itemsSource.GetAllItems().Where(i => i is NodeViewModel);
			foreach (IItem node in nodes)
			{
				if (node.CanShow)
				{
					if (IsNodeInViewBox(node, viewBox, moveOffset))
					{
						return true;
					}
				}
			}

			return false;
		}


		private static bool IsNodeInViewBox(IItem node, Rect viewBox, Vector moveOffset)
		{
			Point location = node.ItemBounds.Location;
			Size size = node.ItemBounds.Size;

			Rect newBounds = new Rect(location + moveOffset, size);
			if (viewBox.IntersectsWith(newBounds))
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