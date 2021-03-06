﻿using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Media;
using Dependinator.ModelViewing.Private.ModelHandling.Core;
using Dependinator.ModelViewing.Private.Nodes;
using Dependinator.Utils.UI;


namespace Dependinator.ModelViewing.Private.Lines.Private
{
    internal class LineControlService : ILineControlService
    {
        private readonly IGeometryService geometryService;
        private readonly Lazy<ILineViewModelService> lineViewModelService;


        public LineControlService(
            Lazy<ILineViewModelService> lineViewModelService,
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


        public void MoveLinePoint(Line line, int pointIndex, Point newPoint, double scale)
        {
            // NOTE: These lines are currently disabled !!!
            NodeViewModel source = line.Source.ViewModel;
            NodeViewModel target = line.Target.ViewModel;

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
                if (geometryService.GetDistanceFromLine(a, b, p) < 0.2)
                {
                    newPoint = geometryService.GetClosestPointOnLineSegment(a, b, p);
                }
                else
                {
                    double roundTo = scale < 10 ? 5 : 1;
                    newPoint = newPoint.Rnd(roundTo);
                }
            }

            line.View.Points[pointIndex] = newPoint;
            lineViewModelService.Value.SetIsChanged(line.View.ViewModel);
        }


        public void RemovePoint(Line line)
        {
            Point canvasPoint = line.View.ViewModel.ItemOwnerCanvas.MouseToCanvasPoint();
            int index = GetLinePointIndex(line, canvasPoint, true);

            List<Point> viewPoints = line.View.Points;

            if (index > 0 && index < viewPoints.Count - 1)
            {
                viewPoints.RemoveAt(index);

                lineViewModelService.Value.UpdateLineBounds(line);
                line.View.ViewModel.NotifyAll();
                lineViewModelService.Value.SetIsChanged(line.View.ViewModel);
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

            return -1;
        }


        public void UpdateLineBounds(Line line)
        {
            lineViewModelService.Value.UpdateLineBounds(line);
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
