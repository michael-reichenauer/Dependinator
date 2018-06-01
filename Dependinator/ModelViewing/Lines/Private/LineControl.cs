using System.Windows;
using System.Windows.Input;
using Dependinator.ModelViewing.ModelHandling.Core;
using Dependinator.Utils;


namespace Dependinator.ModelViewing.Lines.Private
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

		public void MouseDown(MouseEventArgs e)
		{
			mouseDownPoint = line.View.ViewModel.ItemOwnerCanvas.MouseEventToCanvasPoint(e);
			currentPointIndex = -1;
		}


		public void MouseUp(MouseEventArgs e)
		{
			if (currentPointIndex != -1)
			{
				EndMoveLinePoint(e);
			}
			else
			{
				Log.Debug("Mouse click");
			}
		}


		public void MouseMove(bool isPointMove, MouseEventArgs e)
		{
			Point point = line.View.ViewModel.ItemOwnerCanvas.MouseEventToCanvasPoint(e);

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
			double lineScale = line.View.ViewModel.ItemParentScale;
			lineControlService.MoveLinePoint(line, currentPointIndex, point, lineScale);
			lineControlService.UpdateLineBounds(line);
			line.View.ViewModel.NotifyAll();
		}


		private void EndMoveLinePoint(MouseEventArgs e)
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