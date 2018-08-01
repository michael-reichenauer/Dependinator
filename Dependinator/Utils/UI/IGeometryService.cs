using System.Windows;


namespace Dependinator.Utils.UI
{
    internal interface IGeometryService
    {
        double GetDistanceFromLine(Point a, Point b, Point p);
        Point GetPointInPerimeter(Rect rect, Point point);
        Point GetClosestPointOnLineSegment(Point a, Point b, Point p);
    }
}
