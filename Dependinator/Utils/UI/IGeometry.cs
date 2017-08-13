using System.Windows;

namespace Dependinator.ModelViewing.Links.Private
{
	internal interface IGeometry
	{
		double GetDistanceFromLine(Point a, Point b, Point p);
		Point GetPointInPerimeter(Rect rect, Point point);
		Point GetClosestPointOnLineSegment(Point a, Point b, Point p);
	}
}