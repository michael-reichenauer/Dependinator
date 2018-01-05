using System.Collections.Generic;
using System.Windows;
using System.Windows.Input;
using Dependinator.ModelViewing.ModelHandling.Core;
using Dependinator.ModelViewing.Nodes;
using Dependinator.Utils.UI;


namespace Dependinator.ModelViewing.Lines.Private
{
	internal class LineControlService : ILineControlService
	{
		private readonly ILineViewModelService lineViewModelService;
		private readonly IGeometryService geometryService;


		public LineControlService(ILineViewModelService lineViewModelService,
			IGeometryService geometryService)
		{
			this.lineViewModelService = lineViewModelService;
			this.geometryService = geometryService;
		}

		

		public bool IsOnLineBetweenNeighbors(Line line, int index)
		{
			Point p = line.View.Points[index];
			Point a = line.View.Points[index - 1];
			Point b = line.View.Points[index + 1];

			double length = geometryService.GetDistanceFromLine(a, b, p);
			return length < 0.1;

		}


		public void MoveLinePoint(Line line, int pointIndex, Point newPoint)
		{
			// NOTE: These lines are currently disabled !!!
			NodeViewModel source = line.Source.View.ViewModel;
			NodeViewModel target = line.Target.View.ViewModel;

			if (pointIndex == line.View.FirstIndex)
			{
				// Adjust point to be on the source node perimeter
				newPoint = geometryService.GetPointInPerimeter(source.ItemBounds, newPoint);
				line.View.RelativeSourcePoint = new Point(
					(newPoint.X - source.ItemBounds.X) / source.ItemBounds.Width,
					(newPoint.Y - source.ItemBounds.Y) / source.ItemBounds.Height);
			}
			else if (pointIndex == line.View.LastIndex)
			{
				// Adjust point to be on the target node perimeter
				newPoint = geometryService.GetPointInPerimeter(target.ItemBounds, newPoint);
				line.View.RelativeTargetPoint = new Point(
					(newPoint.X - target.ItemBounds.X) / target.ItemBounds.Width,
					(newPoint.Y - target.ItemBounds.Y) / target.ItemBounds.Height);
			}
			else
			{
				Point a = line.View.Points[pointIndex - 1];
				Point b = line.View.Points[pointIndex + 1];
				Point p = newPoint;
				if (geometryService.GetDistanceFromLine(a, b, p) < 0.1)
				{
					newPoint = geometryService.GetClosestPointOnLineSegment(a, b, p);
				}
			}

			line.View.Points[pointIndex] = newPoint;
		}

		public void RemovePoint(Line line)
		{
			Point screenPoint = Mouse.GetPosition(Application.Current.MainWindow);
			Point canvasPoint = line.View.ViewModel.ItemOwnerCanvas.RootScreenToCanvasPoint(screenPoint);
			int index = GetLinePointIndex(line, canvasPoint, true);

			List<Point> viewPoints = line.View.Points;

			if (index > 0 && index < viewPoints.Count - 1)
			{
				viewPoints.RemoveAt(index);

				lineViewModelService.UpdateLineBounds(line);
				line.View.ViewModel.NotifyAll();
			}
		}


		public int GetLinePointIndex(Line line, Point point, bool isPointMove)
		{
			IList<Point> points = line.View.Points;
			double itemScale = line.View.ViewModel.ItemScale;

			// The point is sometimes a bit "off" the line so find the closet point on the line
			Point pointOnLine = GetClosetPointOnlIne(point, points, itemScale);
			point = pointOnLine;


			if (isPointMove && points.Count > 2)
			{
				int index = -1;
				double dist = double.MaxValue;

				for (int i = 1; i < points.Count - 1; i++)
				{
					double currentDist = (point - points[i]).Length;
					if (currentDist < dist)
					{
						index = i;
						dist = currentDist;
					}
				}

				return index;
			}
			else
			{
				for (int i = 0; i < points.Count - 1; i++)
				{
					Point segmentStartPoint = points[i];
					Point segmentEndPoint = points[i + 1];

					double distance = geometryService.GetDistanceFromLine(
															segmentStartPoint, segmentEndPoint, point) * itemScale;

					if (distance < 5)
					{
						// The point is on the segment
						points.Insert(i + 1, point);
						return i + 1;
					}
				}
			}

			return -1;
		}


		public void UpdateLineBounds(Line line)
		{
			lineViewModelService.UpdateLineBounds(line);
		}


		private Point GetClosetPointOnlIne(Point p, IList<Point> points, double itemScale)
		{
			double minDistance = double.MaxValue;
			Point pointOnLine = new Point(0, 0);

			// Iterate the segments to find the segment closest to the point and on that segment, the 
			// closest point
			for (int i = 0; i < points.Count - 1; i++)
			{
				Point a = points[i];
				Point b = points[i + 1];

				double distanceToSegment = geometryService.GetDistanceFromLine(a, b, p) * itemScale;

				if (distanceToSegment < minDistance)
				{
					minDistance = distanceToSegment;
					pointOnLine = geometryService.GetClosestPointOnLineSegment(a, b, p);
				}
			}

			return pointOnLine;
		}
	}
}