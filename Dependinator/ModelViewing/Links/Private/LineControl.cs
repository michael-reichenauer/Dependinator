using System.Windows;
using System.Windows.Input;
using Dependinator.ModelViewing.ModelHandling.Core;
using Dependinator.Utils;


namespace Dependinator.ModelViewing.Links.Private
{
	internal class LineControl
	{
		private readonly ILineControlService lineControlService;
		private readonly Line line;


		private Point mouseDownPoint;
		private int currentPointIndex = -1;

		public LineControl(ILineControlService lineControlService, Line line)
		{
			this.lineControlService = lineControlService;
			this.line = line;
		}


		public void RemovePoint() => lineControlService.RemovePoint(line);

		public void MovePoints(Vector moveOffset)
		{
			for (int i = 1; i < line.View.Points.Count - 1; i++)
			{
				line.View.Points[i] = line.View.Points[i] + moveOffset;
			}
		}

		public void MouseDown(Point screenPoint)
		{
			mouseDownPoint = line.View.ViewModel.ItemOwnerCanvas.RootScreenToCanvasPoint(screenPoint);
			currentPointIndex = -1;
		}


		public void MouseUp(Point screenPoint)
		{
			if (currentPointIndex != -1)
			{
				EndMoveLinePoint();
			}
			else
			{
				Log.Debug("Mouse click");
			}
		}


		public void MouseMove(Point screenPoint, bool isPointMove)
		{
			Point point = line.View.ViewModel.ItemOwnerCanvas.RootScreenToCanvasPoint(screenPoint);

			if (currentPointIndex == -1)
			{
				// First move event, lets start a move by  getting the index of point to move.
				// THis might create a new point if there is no existing point near the mouse down point
				currentPointIndex = lineControlService.GetLinePointIndex(
					line, mouseDownPoint, isPointMove);
				if (currentPointIndex == -1)
				{
					// Point not close enough to the line
					return;
				}
			}

			Mouse.OverrideCursor = Cursors.SizeAll;
			lineControlService.MoveLinePoint(line, currentPointIndex, point);
			lineControlService.UpdateLineBounds(line);
			line.View.ViewModel.NotifyAll();
		}



		private void EndMoveLinePoint()
		{
			if (currentPointIndex != line.View.FirstIndex && currentPointIndex != line.View.LastIndex)
			{
				// Removing the point if it is no longer needed (in the same line as neighbors points
				if (lineControlService.IsOnLineBetweenNeighbors(line, currentPointIndex))
				{
					line.View.Points.RemoveAt(currentPointIndex);
				}
			}

			Mouse.OverrideCursor = null;
			lineControlService.UpdateLineBounds(line);
			line.View.ViewModel.NotifyAll();
			currentPointIndex = -1;
		}
	}
}