﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using Dependinator.ModelViewing.Private.Items.Private;
using Dependinator.Utils;
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

		private ItemsCanvas canvasParent;
		private ZoomableCanvas zoomableCanvas;
		//private static readonly int MaxZoomScale = 20_0000000;

		private Rect ItemsCanvasBounds =>
			owner?.ItemBounds ?? zoomableCanvas?.ActualViewbox ?? Rect.Empty;


		private bool IsShowing => owner?.IsShowing ?? true;
		private bool CanShow => owner?.CanShow ?? true;


		// The root canvas
		public ItemsCanvas()
			: this(null)
		{
			CanvasRoot = this;
			Scale = 1;
			ScaleFactor = 1;
		}

		public ItemsCanvas(IItemsCanvasBounds owner)
		{
			this.owner = owner;
			itemsSource = new ItemsSource(this);
			Scale = 1 * DefaultScaleFactor;
		}

		public double ParentScale => IsRoot ? Scale : canvasParent.ParentScale * canvasParent.ScaleFactor;

		public ItemsCanvas CanvasRoot { get; private set; }

		public IReadOnlyList<ItemsCanvas> CanvasChildren => canvasChildren;

		public bool IsRoot => canvasParent == null;

		public double ScaleFactor { get; private set; } = DefaultScaleFactor;


		public void AddChildCanvas(ItemsCanvas childCanvas)
		{
			childCanvas.canvasParent = this;
			childCanvas.CanvasRoot = CanvasRoot;

			childCanvas.Scale = Scale * childCanvas.ScaleFactor;
			canvasChildren.Add(childCanvas);
		}


		public void RemoveChildCanvas(ItemsCanvas childCanvas)
		{
			childCanvas.canvasParent = null;
			canvasChildren.Remove(childCanvas);
		}


		public bool IsZoomAndMoveEnabled { get; set; } = true;

		public void ResetLayout()
		{
			if (!IsRoot)
			{
				Scale = ParentScale * DefaultScaleFactor;
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

				if (canvasParent != null)
				{
					ScaleFactor = Scale / canvasParent.Scale;
				}

				if (zoomableCanvas != null)
				{
					zoomableCanvas.Scale = value;
				}
			}
		}


		public void ZoomRootNode(double zoom, Point? zoomCenter = null)
		{
			CanvasRoot.ZoomImpl(zoom, zoomCenter);
		}


		public void ZoomNode(double zoom, Point? zoomCenter = null)
		{
			ZoomImpl(zoom, zoomCenter);
		}


		private void ZoomImpl(double zoom, Point? zoomCenter = null)
		{
			if (!IsZoomAndMoveEnabled)
			{
				return;
			}

			double newScale = Scale * zoom;
			if (!IsShowing || !CanShow || IsRoot && newScale < 0.15 && zoom < 1)
			{
				// Item not shown or reached minimum root zoom level
				return;
			}

			double zoomFactor = newScale / Scale;
			Scale = newScale;

			// Adjust the offset to make the point at the center of zoom area stay still (if provided)
			if (zoomCenter.HasValue)
			{
				Vector position = (Vector)zoomCenter;
				Offset = (Point)((Vector)(Offset + position) * zoomFactor - position);
			}


			UpdateAndNotifyAll();

			canvasChildren.ForEach(child => child.UpdateScaleWitPos());
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


		private void UpdateScaleWitPos()
		{
			double newScale = ParentScale * ScaleFactor;
			double zoom = newScale / Scale;

			if (Math.Abs(zoom) > 0.001)
			{
				//Point center = new Point(1, 1);

				ZoomImpl(zoom, null);
			}
		}


		private void UpdateScale()
		{
			double newScale = ParentScale * ScaleFactor;
			double zoom = newScale / Scale;

			if (Math.Abs(zoom) > 0.001)
			{
				ZoomImpl(zoom, null);
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

			Point parentCanvasPoint = canvasParent.RootScreenToCanvasPoint(rootScreenPoint);

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
				Rect parentArea = canvasParent.GetHierarchicalVisualArea();
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