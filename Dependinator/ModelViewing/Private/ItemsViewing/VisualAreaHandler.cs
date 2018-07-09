using System;
using System.Windows;
using Dependinator.Utils;


namespace Dependinator.ModelViewing.Private.ItemsViewing
{
	/// <summary>
	/// Handles visual area calculations while compensating for ancestors view areas
	/// </summary>
	internal class VisualAreaHandler
	{
		private readonly ItemsCanvas itemsCanvas;


		public VisualAreaHandler(ItemsCanvas itemsCanvas)
		{
			this.itemsCanvas = itemsCanvas;
		}


		public Rect GetVisualArea(Rect viewArea)
		{
			if (itemsCanvas.IsRoot)
			{
				return viewArea;
			}

			if (viewArea == Rect.Empty)
			{
				return viewArea;
			}

			// Adjust view area to compensate for ancestors view areas as well
			Rect ancestorsViewArea = GetHierarchicalVisualArea(itemsCanvas);
			viewArea.Intersect(ancestorsViewArea);

			return viewArea;
		}


		/// <summary>
		/// Returns the visual area after all ancestors view areas have been intersected
		/// </summary>
		private static Rect GetHierarchicalVisualArea(ItemsCanvas canvas)
		{
			if (canvas.IsRoot)
			{
				// Reached root, return the root canvas view area
				return GetItemsCanvasViewArea(canvas);
			}

			if (null == PresentationSource.FromVisual(canvas.ZoomableCanvas))
			{
				// This canvas is not showing
				return Rect.Empty;
			}

			Rect parentArea = GetHierarchicalVisualArea(canvas.ParentCanvas);

			Point parentCanvasPoint = parentArea.Location;

			Point childCanvasPoint = ParentToChildCanvasPoint(canvas, parentCanvasPoint);

			Size parentSizeInChildCanvasSize = (Size) ((Vector) parentArea.Size / canvas.ScaleFactor);
			Rect parentViewAreaInChildCanvasArea = new Rect(childCanvasPoint, parentSizeInChildCanvasSize);

			// Intersect parent area with child(this) area
			Rect viewArea = GetItemsCanvasViewArea(canvas);
			viewArea.Intersect(parentViewAreaInChildCanvasArea);
			return viewArea;
		}



		private static Rect GetItemsCanvasViewArea(ItemsCanvas canvas)
		{
			double scale = canvas.Scale;
			double parentScale = canvas.ParentCanvas?.Scale ?? scale;

			Size renderSize = (Size)((Vector)canvas.ItemsCanvasBounds.Size * parentScale);

			double x = 0;
			double y = 0;

			if (canvas.IsRoot)
			{
				x = canvas.ZoomableCanvas.Offset.X;
				y = canvas.ZoomableCanvas.Offset.Y;
			}

			Rect value = new Rect(
				x / scale, 
				y / scale, 
				renderSize.Width / scale,
				renderSize.Height / scale);

			return value;
		}


		private static Point ParentToChildCanvasPoint(ItemsCanvas canvas, Point parentCanvasPoint)
		{
			Point parentScreenPoint = canvas.ParentCanvas.CanvasToScreenPoint(parentCanvasPoint);
			return ScreenToCanvasPoint(canvas, parentScreenPoint);
		}


		private static Point ScreenToCanvasPoint(ItemsCanvas canvas, Point screenPoint)
		{
			try
			{
				Point localScreenPoint = canvas.ZoomableCanvas.PointFromScreen(screenPoint);

				Point canvasPoint = canvas.ZoomableCanvas.GetCanvasPoint(localScreenPoint);

				return canvasPoint;
			}
			catch (Exception e)
			{
				Log.Exception(e, $"Node {canvas}");
				throw;
			}
		}
	}
}