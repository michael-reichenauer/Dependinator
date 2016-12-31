using System;
using System.Windows;
using System.Windows.Input;
using Dependiator.Utils;
using Dependiator.Utils.UI.VirtualCanvas;


namespace Dependiator.MainViews
{
	[SingleInstance]
	public class CanvasService : ICanvasService
	{
		private ZoomableCanvas canvas;

		public void SetCanvas(ZoomableCanvas zoomableCanvas)
		{
			canvas = zoomableCanvas;
		}


		public bool ZoomCanvas(int zoomDelta, Point currentPosition)
		{
			// Adjust X in "zoomDelta / X" to adjust zoom speed
			double zoom = Math.Pow(2, zoomDelta / 10.0 / Mouse.MouseWheelDeltaForOneLine);

			double newScale = canvas.Scale * zoom;

			Log.Debug($"Zoom {zoom}, scale {canvas.Scale}, offset {canvas.Offset}");
			if (newScale < 0.1 || newScale > 5)
			{
				Log.Warn($"Zoom to large");
				return true;
			}

			canvas.Scale = newScale;

			// Adjust the offset to make the point under the mouse stay still.
			currentPosition = new Point(currentPosition.X - 10, currentPosition.Y - 30);
			Vector position = (Vector)currentPosition;
			canvas.Offset = (System.Windows.Point)((Vector)
																						 (canvas.Offset + position) * zoom - position);

			Log.Debug($"Scroll {zoom}, scale {canvas.Scale}, offset {canvas.Offset}");

			return true;
		}


		public bool MoveCanvas(Vector viewOffset)
		{
			canvas.Offset -= viewOffset;
			return true;
		}


		public Point GetCanvasPosition(Point viewPosition)
		{
			double x = viewPosition.X + canvas.Offset.X;
			double y = viewPosition.Y + canvas.Offset.Y;

			Point canvasPosition = new Point(x, y);
			return canvasPosition;
		}

	}
}