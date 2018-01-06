using System;
using System.Linq;
using System.Windows;
using Dependinator.ModelViewing.ModelHandling.Core;


namespace Dependinator.ModelViewing.Lines.Private
{
	internal class LineService : ILineService
	{
		private static readonly Point MiddleBottom = new Point(0.5, 1);
		private static readonly Point MiddleTop = new Point(0.5, 0);
		private static readonly double LineMargin = 10;

		public void UpdateLineEndPoints(Line line)
		{
			Rect source = line.Source.View.ViewModel.ItemBounds;
			Rect target = line.Target.View.ViewModel.ItemBounds;

			Point relativeSource = GetRelativeSource(line);
			Point relativeTarget = GetRelativeTarget(line);

			Point sp = source.Location
			           + new Vector(source.Width * relativeSource.X, source.Height * relativeSource.Y);

			Point tp = target.Location
			           + new Vector(target.Width * relativeTarget.X, target.Height * relativeTarget.Y);

			if (line.Source.Parent == line.Target)
			{
				// From child to parent node
				// Need to adjust for different scales
				Vector vector = line.View.ViewModel.ItemScale > 1
					? new Vector(0, 8 / line.View.ViewModel.ItemScale) : new Vector(0, 8);

				tp = ParentPointToChildPoint(line.Target, tp) - vector;
			}
			else if (line.Source == line.Target.Parent)
			{
				// From parent to child node
				// Need to adjust for different scales
				sp = ParentPointToChildPoint(line.Source, sp);

				Vector vector = line.View.ViewModel.ItemScale > 1
					? new Vector(0, 7 / line.View.ViewModel.ItemScale) : new Vector(0, 7);
				tp = tp - vector;
			}
			else
			{
				// Siblings
				Vector vector = line.View.ViewModel.ItemScale > 1
					? new Vector(0, 7 / line.View.ViewModel.ItemScale) : new Vector(0, 7);
				tp = tp - vector;
			}

			line.View.FirstPoint = sp;
			line.View.LastPoint = tp;
		}


		public void UpdateLineBounds(Line line)
		{
			Rect bounds = new Rect(line.View.FirstPoint, line.View.LastPoint);

			// Adjust boundaries for line points between first and last point
			line.View.MiddlePoints().ForEach(point => bounds.Union((Point) point));

			// The items bound needs some margin around the line to allow line width and arrow to show
			double margin = LineMargin / line.View.ViewModel.ItemScale;
			bounds.Inflate(margin, margin);

			// Set the new bounds
			line.View.ViewModel.ItemBounds = bounds;
		}




		public string GetLineData(Line line)
		{
			Point s = Scaled(line, line.View.FirstPoint);
			Point t = Scaled(line, line.View.LastPoint);

			return Txt.I($"M {s.X},{s.Y} {GetMiddleLineData(line)} L {t.X},{t.Y - 10} L {t.X},{t.Y - 6.5}");
		}


		private string GetMiddleLineData(Line line)
		{
			string lineData = "";

			foreach (Point point in line.View.MiddlePoints())
			{
				Point m = Scaled(line, point);
				lineData += Txt.I($" L {m.X},{m.Y}");
			}

			return lineData;
		}


		public string GetPointsData(Line line)
		{
			string lineData = "";
			double d = 2;

			foreach (Point point in line.View.MiddlePoints())
			{
				Point m = Scaled(line, point);
				lineData += Txt.I(
					$" M {m.X - d},{m.Y - d} H {m.X + d} V {m.Y + d} H {m.X - d} V {m.Y - d} H {m.X + d} ");
			}

			return lineData;
		}


		public string GetEndPointsData(Line line)
		{
			string lineData = "";
			double d = 2;

			foreach (Point point in new[] { line.View.FirstPoint, line.View.LastPoint })
			{
				Point m = Scaled(line, point);
				lineData += Txt.I(
					$" M {m.X - d},{m.Y - d} H {m.X + d} V {m.Y + d} H {m.X - d} V {m.Y - d} H {m.X + d} ");
			}

			return lineData;
		}


		public string GetArrowData(Line line)
		{
			Point t = Scaled(line, line.View.LastPoint);

			return Txt.I($"M {t.X},{t.Y - 6.5} L {t.X},{t.Y - 4.5}");
		}

		private Point Scaled(Line line, Point p)
		{
			Rect bounds = line.View.ViewModel.ItemBounds;
			double scale = line.View.ViewModel.ItemScale;
			return new Point((p.X - bounds.X) * scale, (p.Y - bounds.Y) * scale);
		}


		public double GetLineWidth(Line line)
		{
			double scale = line.View.ViewModel.ItemScale;
			double lineWidth;

			int linksCount = line.Links.Any() ? line.Links.Count : line.LinkCount;

			if (linksCount < 5)
			{
				lineWidth = 1;
			}
			else if (linksCount < 15)
			{
				lineWidth = 4;
			}
			else
			{
				lineWidth = 6;
			}

			double lineLineWidth = (lineWidth * 0.7 * scale).MM(0.1, 4);

			if (line.View.ViewModel.IsMouseOver)
			{
				lineLineWidth = (lineLineWidth * 1.5).MM(0, 6);
			}

			return lineLineWidth;
		}


		public double GetArrowWidth(Line line)
		{
			double arrowWidth = (10 * line.View.ViewModel.ItemScale).MM(6, 15);

			if (line.View.ViewModel.IsMouseOver)
			{
				arrowWidth = (arrowWidth * 1.5).MM(0, 20);
			}

			return arrowWidth;
		}



		private Point GetRelativeSource(Line line)
		{
			if (line.View.RelativeSourcePoint.X >= 0)
			{
				// use specified source
				return line.View.RelativeSourcePoint;
			}

			if (line.Source == line.Target.Parent)
			{
				// The target is the child of the source,
				// i.e. line start at the top of the source and goes to target top
				return MiddleTop;
			}

			// If target is sibling or parent
			// i.e. line start at the bottom of the source and goes to target top
			return MiddleBottom;
		}



		private Point GetRelativeTarget(Line line)
		{
			if (line.View.RelativeTargetPoint.X >= 0)
			{
				// use specified source
				return line.View.RelativeTargetPoint;
			}

			if (line.Source.Parent == line.Target)
			{
				// The target is a parent of the source,
				// i.e. line starts at source bottom and ends at the bottom of the target node
				return MiddleBottom;
			}

			// If target is sibling or child
			// i.e. line start at the bottom of the source and goes to target top
			return MiddleTop;
		}


		private static Point ParentPointToChildPoint(Node parent, Point point)
		{
			return parent.View.ItemsCanvas.ParentToChildCanvasPoint(point);
		}
	}
}