﻿using System;
using System.Linq;
using System.Windows;
using System.Windows.Input;
using Dependiator.MainViews.Private;
using Dependiator.Utils;
using Dependiator.Utils.UI.VirtualCanvas;


namespace Dependiator.MainViews
{
	[SingleInstance]
	internal class CanvasService : ICanvasService
	{
		private static readonly double ZoomSpeed = 600.0;

		private ZoomableCanvas canvas;

		public event EventHandler ScaleChanged;



		public void SetCanvas(ZoomableCanvas zoomableCanvas)
		{
			canvas = zoomableCanvas;
		}


		public double Scale => canvas.Scale;


		public bool ZoomCanvas(int zoomDelta, Point viewPosition)
		{		
			double zoom = Math.Pow(2, zoomDelta / ZoomSpeed);

			double maxScale = 20;
			double minScale = GetMinScale();

			double newScale = canvas.Scale * zoom;

			// Limit zooming
			if (newScale < minScale)
			{
				newScale = minScale;
				
			}
			else if (newScale > maxScale)
			{
				newScale = maxScale;
			}

			if (newScale == canvas.Scale)
			{
				return true;
			}

			zoom = newScale / canvas.Scale;

			canvas.Scale = newScale;

			// Adjust the offset to make the point under the mouse stay still.
			Vector position = (Vector)viewPosition;
			canvas.Offset = (Point)((Vector)(canvas.Offset + position) * zoom - position);

			// Log.Debug($"Scroll {zoom}, scale {canvas.Scale}, offset {canvas.Offset}");

			ScaleChanged?.Invoke(this, EventArgs.Empty);

			return true;
		}


		private double GetMinScale()
		{
			double minScaleWidth = canvas.ActualWidth / canvas.Extent.Width;
			double minScaleHeight = canvas.ActualHeight / canvas.Extent.Height;
			double minScale = Math.Min(minScaleWidth, minScaleHeight);
			return minScale;
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


		public Point GetViewPosition(Point canvasPosition)
		{
			double x = canvasPosition.X - canvas.Offset.X;
			double y = canvasPosition.Y - canvas.Offset.Y;

			Point viewPosition = new Point(x, y);
			return viewPosition;
		}
	}
}