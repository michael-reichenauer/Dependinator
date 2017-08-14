using System;
using System.Windows;

namespace Dependinator.Utils.UI
{
	internal class GeometryService : IGeometryService
	{

		public double GetDistanceFromLine(Point a, Point b, Point p)
		{
			double aB = (b - a).Length;
			double aP = (p - a).Length;
			double pB = (b - p).Length;

			return Math.Abs(aB - (aP + pB));
		}


		public Point GetPointInPerimeter(Rect rect, Point point)
		{
			double r = rect.X + rect.Width;
			double b = rect.Y + rect.Height;

			double x = point.X.MM(rect.X, r);
			double y = point.Y.MM(rect.Y, b);

			double dl = Math.Abs(x - rect.X);
			double dr = Math.Abs(x - r);
			double dt = Math.Abs(y - rect.Y);
			double db = Math.Abs(y - b);

			double m = Math.Min(Math.Min(Math.Min(dl, dr), dt), db);

			if (Math.Abs(m - dt) < 0.01) return new Point(x, rect.Y);

			if (Math.Abs(m - db) < 0.01) return new Point(x, b);

			if (Math.Abs(m - dl) < 0.01) return new Point(rect.X, y);

			return new Point(r, y);
		}



		public Point GetClosestPointOnLineSegment(Point a, Point b, Point p)
		{
			Vector ap = p - a;       //Vector from A to P   
			Vector ab = b - a;       //Vector from A to B  

			double magnitudeAb = ab.LengthSquared;     //Magnitude of AB vector (it's length squared) 
			double abpProduct = ap.X * ab.X + ap.Y * ab.Y; // The dot product of a_to_p and a_to_b
			double distance = abpProduct / magnitudeAb; //The normalized "distance" from a to your closest point  

			if (distance < 0)     //Check if P projection is over vectorAB     
			{
				return a;
			}
			else if (distance > 1)
			{
				return b;
			}
			else
			{
				return a + ab * distance;
			}
		}

	}
}