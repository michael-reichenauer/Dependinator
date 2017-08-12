using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using Dependinator.ModelViewing.Nodes;

namespace Dependinator.ModelViewing.Links.Private
{
	internal class LineViewModelService : ILineViewModelService
	{
		private static readonly double LineMargin = 10;

		private readonly ILinkSegmentService segmentService;

		private readonly Point middleBottom = new Point(0.5, 1);
		private readonly Point middleTop = new Point(0.5, 0);

		public LineViewModelService(ILinkSegmentService segmentService)
		{
			this.segmentService = segmentService;
		}



		public void UpdateLineEndPoints(Line line)
		{
			Rect source = line.Source.ViewModel.ItemBounds;
			Rect target = line.Target.ViewModel.ItemBounds;

			Point relativeSource = GetRelativeSource(line.Source, line.Target, line.SourceAdjust);
			Point relativeTarget = GetRelativeTarget(line.Source, line.Target, line.TargetAdjust);

			Point sp = source.Location + new Vector(
				           source.Width * relativeSource.X, source.Height * relativeSource.Y);

			Point tp = target.Location + new Vector(
				           target.Width * relativeTarget.X, target.Height * relativeTarget.Y);

			if (line.Source.Parent == line.Target)
			{
				// Need to adjust for different scales
				tp = ParentPointToChildPoint(line.Target, tp);
			}
			else if (line.Source == line.Target.Parent)
			{
				// Need to adjust for different scales
				sp = ParentPointToChildPoint(line.Source, sp);
			}

			line.FirstPoint = sp;
			line.LastPoint = tp;
		}


		public void UpdateLineBounds(Line line)
		{
			Point sp = line.FirstPoint;
			Point tp = line.LastPoint;

			// Calculate the line boundaries bases on first an last point
			double x = Math.Min(sp.X, tp.X);
			double y = Math.Min(sp.Y, tp.Y);
			double width = Math.Abs(tp.X - sp.X);
			double height = Math.Abs(tp.Y - sp.Y);

			Rect bounds = new Rect(x, y, width, height);

			// Adjust boundaries for line points between first and last
			line.MiddlePoints().ForEach(point => bounds.Union(point));

			// The items bound has some margin around the line to allow full line width and arrow to show
			double scale = line.ViewModel.ItemScale;
			line.ViewModel.ItemBounds = new Rect(
				bounds.X - LineMargin / scale,
				bounds.Y - (LineMargin) / scale,
				bounds.Width + (LineMargin * 2) / scale,
				bounds.Height + (LineMargin * 2) / scale);
		}



		public bool IsOnLineBetweenNeighbors(Line line, int index)
		{
			Point p = line.Points[index];
			Point a = line.Points[index - 1];
			Point b = line.Points[index + 1];

			double length = GetDistanceFromLine(a, b, p);
			return length < 0.1;

		}


		public void MoveLinePoint(Line line, int pointIndex, Point newPoint)
		{
			// NOTE: These lines are currently disabled !!!
			NodeViewModel source = line.Source.ViewModel;
			NodeViewModel target = line.Target.ViewModel;

			if (pointIndex == line.FirstIndex)
			{
				// Adjust point to be on the source node perimeter
				newPoint = GetPointInPerimeter(source.ItemBounds, newPoint);
				line.SourceAdjust = new Point(
					(newPoint.X - source.ItemBounds.X) / source.ItemBounds.Width,
					(newPoint.Y - source.ItemBounds.Y) / source.ItemBounds.Height);
			}
			else if (pointIndex == line.LastIndex)
			{
				// Adjust point to be on the target node perimeter
				newPoint = GetPointInPerimeter(target.ItemBounds, newPoint);
				line.TargetAdjust = new Point(
					(newPoint.X - target.ItemBounds.X) / target.ItemBounds.Width,
					(newPoint.Y - target.ItemBounds.Y) / target.ItemBounds.Height);
			}
			else
			{
				Point p = newPoint;
				Point a = line.Points[pointIndex - 1];
				Point b = line.Points[pointIndex + 1];
				if (GetDistanceFromLine(a, b, p) < 0.1)
				{
					newPoint = GetClosestPointOnLineSegment(a, b, p);
				}
			}

			line.Points[pointIndex] = newPoint;
		}



		public int GetLinePointIndex(Line line, Point p)
		{
			IList<Point> points = line.Points;
			double itemScale = line.ViewModel.ItemScale;

			for (int i = 0; i < points.Count - 1; i++)
			{
				Point a = points[i];
				Point b = points[i + 1];

				if ((p - a).Length * itemScale < 10)
				{
					// The point is close to a (skipping first point)
					if (i != 0)
					{
						return i;
					}
				}
				else if ((p - b).Length * itemScale < 10)
				{
					// The point is close to b (skipping last point)
					if (i + 1 != points.Count - 1)
					{
						return i + 1;
					}
				}

				double length = GetDistanceFromLine(a, b, p) * itemScale;
				if (length < 5)
				{
					// The point p is on the line between point a and b
					points.Insert(i + 1, p);
					return i + 1;
				}
			}

			return -1;
		}


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


		private Point GetRelativeSource(Node sourceNode, Node targetNode, Point relativePoint)
		{
			if (relativePoint.X >= 0)
			{
				// use specified source
				return relativePoint;
			}

			if (sourceNode == targetNode.Parent)
			{
				// The target is the child of the source,
				// i.e. line start at the top of the source and goes to target top
				return middleTop;
			}

			// If target is sibling or parent
			// i.e. line start at the bottom of the source and goes to target top
			return middleBottom;
		}



		private Point GetRelativeTarget(Node sourceNode, Node targetNode, Point relativePoint)
		{
			if (relativePoint.X >= 0)
			{
				// use specified source
				return relativePoint;
			}

			if (sourceNode.Parent == targetNode)
			{
				// The target is a parent of the source,
				// i.e. line starts at source bottom and ends at the bottom of the target node
				return middleBottom;
			}

			// If target is sibling or child
			// i.e. line start at the bottom of the source and goes to target top
			return middleTop;
		}


		public string GetLineData(Line line)
		{
			Point s = Scaled(line, line.FirstPoint);
			Point t = Scaled(line, line.LastPoint);

			return Txt.I($"M {s.X},{s.Y} {GetMiddleLineData(line)} L {t.X},{t.Y - 10} L {t.X},{t.Y - 6.5}");
		}


		private string GetMiddleLineData(Line line)
		{
			string lineData = "";

			foreach (Point point in line.MiddlePoints())
			{
				Point m = Scaled(line, point);
				lineData += Txt.I($" L {m.X},{m.Y}");
			}

			return lineData;
		}


		public string GetPointsData(Line line)
		{
			string lineData = "";
			double d = GetLineWidth(line).MM(0.5, 4);

			foreach (Point point in line.MiddlePoints())
			{
				Point m = Scaled(line, point);
				lineData += Txt.I(
					$" M {m.X - d},{m.Y - d} H {m.X + d} V {m.Y + d} H {m.X - d} V {m.Y - d} H {m.X + d} ");
			}

			return lineData;
		}


		public string GetArrowData(Line line)
		{
			Point t = Scaled(line, line.LastPoint);

			return Txt.I($"M {t.X},{t.Y - 6.5} L {t.X},{t.Y - 4.5}");
		}

		private Point Scaled(Line line, Point p)
		{
			Rect bounds = line.ViewModel.ItemBounds;
			double scale = line.ViewModel.ItemScale;
			return new Point((p.X - bounds.X) * scale, (p.Y - bounds.Y) * scale);
		}


		public double GetLineWidth(Line line)
		{
			double scale = line.ViewModel.ItemScale;
			double lineWidth;

			int linksCount = line.Links.Count;

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

			if (line.ViewModel.IsMouseOver)
			{
				lineLineWidth = (lineLineWidth * 1.5).MM(0, 6);
			}

			return lineLineWidth;
		}


		public string GetLineToolTip(Line line)
		{
			string tip = "";

			IReadOnlyList<LinkGroup> linkGroups = GetLinkGroups(line);

			var groupBySources = linkGroups.GroupBy(link => link.Source);

			foreach (var group in groupBySources)
			{
				tip += $"\n  {group.Key} ->";

				foreach (LinkGroup linkGroup in group)
				{
					tip += $"\n           -> {linkGroup.Target} ({linkGroup.Links.Count})";
				}
			}

			tip = tip.Substring(1); // Skipping first "\n"
			return tip;
		}


		//public (Point source, Point target) GetLineEndPoints2(
		//	Node source, Node target, Rect sourceAdjust, Rect targetAdjust)
		//{
		//	Rect sourceBounds = source.ViewModel.ItemBounds;
		//	Rect targetBounds = target.ViewModel.ItemBounds;

		//	if (source.Parent == target.Parent)
		//	{
		//		// Source and target nodes are siblings, 
		//		// ie. line starts at source middle bottom and ends at target middle top
		//		double x1 = sourceBounds.X + sourceBounds.Width / 2;
		//		double y1 = sourceBounds.Y + sourceBounds.Height;
		//		Point sp = new Point(x1, y1);

		//		double x2 = targetBounds.X + targetBounds.Width / 2;
		//		double y2 = targetBounds.Y;
		//		Point tp = new Point(x2, y2);

		//		return (sp, tp);
		//	}
		//	else if (source.Parent == target)
		//	{
		//		// The target is a parent of the source,
		//		// i.e. line ends at the bottom of the target node
		//		double x1 = sourceBounds.X + sourceBounds.Width / 2;
		//		double y1 = sourceBounds.Y + sourceBounds.Height;
		//		Point sp = new Point(x1, y1);

		//		double x2 = targetBounds.X + targetBounds.Width / 2;
		//		double y2 = targetBounds.Y + targetBounds.Height;
		//		Point tp = ParentPointToChildPoint(target, new Point(x2, y2));

		//		return (sp, tp);
		//	}
		//	else //if (source == target.Parent)
		//	{
		//		// The target is the child of the source,
		//		// i.e. line start at the top of the source
		//		double x1 = sourceBounds.X + sourceBounds.Width / 2;
		//		double y1 = sourceBounds.Y;
		//		Point sp = ParentPointToChildPoint(source, new Point(x1, y1));

		//		double x2 = targetBounds.X + targetBounds.Width / 2;
		//		double y2 = targetBounds.Y;
		//		Point tp = new Point(x2, y2);

		//		return (sp, tp);
		//	}
		//}


		private static Point ParentPointToChildPoint(Node parent, Point point)
		{
			return parent.ItemsCanvas.ParentToChildCanvasPoint(point);
		}


		public void AddLinkLines(LinkOld link)
		{
			//var linkSegments = segmentService.GetNormalLinkSegments(link);

			//linkSegments.ForEach(AddNormalLinkSegment);
			//link.SetLinkSegments(linkSegments);
		}


		//public void ZoomInLinkLine(LinkLine line)
		//{
		//	IReadOnlyList<Link> links = line.Links.ToList();

		//	foreach (Link link in links)
		//	{
		//		IReadOnlyList<LinkSegment> currentLinkSegments = link.LinkSegments.ToList();

		//		var zoomedSegments = segmentService.GetZoomedInReplacedSegments(currentLinkSegments, line.Source, line.Target);

		//		LinkSegment zoomedInSegment = segmentService.GetZoomedInSegment(zoomedSegments, link);

		//		var newSegments = segmentService.GetNewLinkSegments(currentLinkSegments, zoomedInSegment);

		//		var replacedLines = GetLines(link.Lines, zoomedSegments);

		//		replacedLines.ForEach(replacedLine => HideLinkFromLine(replacedLine, link));

		//		AddDirectLine(zoomedInSegment);

		//		link.SetLinkSegments(newSegments);
		//	}

		//	line.Owner.RootNode.UpdateNodeVisibility();
		//}


		public void ZoomInLinkLine(LinkLineOld line, NodeOld node)
		{
			//	IReadOnlyList<LinkOld> links = line.Links.ToList();

			//	foreach (LinkOld link in links)
			//	{
			//		IReadOnlyList<LinkSegmentOld> currentLinkSegments = link.LinkSegments.ToList();

			//		IReadOnlyList<LinkSegmentOld> zoomedSegments;
			//		if (node == line.Target)
			//		{
			//			zoomedSegments = segmentService.GetZoomedInBeforeReplacedSegments(currentLinkSegments, line.Source, line.Target);
			//		}
			//		else
			//		{
			//			zoomedSegments = segmentService.GetZoomedInAfterReplacedSegments(currentLinkSegments, line.Source, line.Target);
			//		}

			//		LinkSegmentOld zoomedInSegment = segmentService.GetZoomedInSegment(zoomedSegments, link);

			//		var newSegments = segmentService.GetNewLinkSegments(currentLinkSegments, zoomedInSegment);

			//		var replacedLines = GetLines(link.Lines, zoomedSegments);

			//		replacedLines.ForEach(replacedLine => HideLinkFromLine(replacedLine, link));

			//		link.SetLinkSegments(newSegments);
			//		if (AddDirectLine(zoomedInSegment))
			//		{
			//			break;
			//		}
			//	}

			//	line.Owner.RootNode.UpdateNodeVisibility();
			//}


			//public void ZoomOutLinkLine(LinkLine line)
			//{ 
			//	IReadOnlyList<Link> links = line.HiddenLinks.ToList();

			//	foreach (Link link in links)
			//	{
			//		IReadOnlyList<LinkSegment> normalLinkSegments = segmentService.GetNormalLinkSegments(link);
			//		IReadOnlyList<LinkSegment> currentLinkSegments = link.LinkSegments.ToList();

			//		var zoomedSegments = segmentService.GetZoomedOutReplacedSegments(normalLinkSegments, currentLinkSegments, line.Source, line.Target);

			//		LinkSegment zoomedInSegment = segmentService.GetZoomedInSegment(zoomedSegments, link);
			//		var replacedLines = GetLines(link.Lines, new [] { zoomedInSegment });
			//		replacedLines.ForEach(replacedLine => HideLinkFromLine(replacedLine, link));

			//		zoomedSegments.ForEach(segment => AddDirectLine(segment));

			//		var newSegments = segmentService.GetNewLinkSegments(currentLinkSegments, zoomedSegments);
			//		link.SetLinkSegments(newSegments);
			//	}

			//	line.Owner.AncestorsAndSelf().Last().UpdateNodeVisibility();
		}


		public void ZoomOutLinkLine(LinkLineOld line, NodeOld node)
		{
			//IReadOnlyList<LinkOld> links = line.Links.ToList();

			//foreach (LinkOld link in links)
			//{
			//	IReadOnlyList<LinkSegmentOld> normalLinkSegments = segmentService.GetNormalLinkSegments(link);
			//	IReadOnlyList<LinkSegmentOld> currentLinkSegments = link.LinkSegments.ToList();

			//	IReadOnlyList<LinkSegmentOld> zoomedSegments = segmentService.GetZoomedOutReplacedSegments(normalLinkSegments, currentLinkSegments, line.Source, line.Target);

			//	if (zoomedSegments.Count > 1)
			//	{
			//		if (node == line.Target)
			//		{
			//			zoomedSegments = zoomedSegments.Skip(1).ToList();
			//		}
			//		else
			//		{
			//			zoomedSegments = zoomedSegments.Take(zoomedSegments.Count - 1).ToList();
			//		}

			//		LinkSegmentOld zoomedInSegment = segmentService.GetZoomedInSegment(zoomedSegments, link);

			//		var newSegments = segmentService.GetNewLinkSegments(normalLinkSegments, zoomedInSegment);

			//		HideLinkFromLine(line, link);

			//		newSegments.ForEach(segment => AddDirectLine(segment));


			//		link.SetLinkSegments(newSegments);
			//	}		
			//}

			//line.Owner.RootNode.UpdateNodeVisibility();
		}


		public void CloseLine(LinkLineOld line)
		{
			line.Owner.RemoveOwnedLineItem(line);
			line.Owner.Links.RemoveOwnedLine(line);

			line.Source.Links.RemoveReferencedLine(line);
			line.Target.Links.RemoveReferencedLine(line);

			line.Source.AncestorsAndSelf()
				.TakeWhile(node => node != line.Owner)
				.ForEach(node => node.Links.RemoveReferencedLine(line));

			line.Target.AncestorsAndSelf()
				.TakeWhile(node => node != line.Owner)
				.ForEach(node => node.Links.RemoveReferencedLine(line));
		}


		private void HideLinkFromLine(LinkLineOld line, LinkOld link)
		{
			line.HideLink(link);
			link.Remove(line);

			if (!line.IsMouseOver && !line.IsNormal && !line.Links.Any())
			{
				CloseLine(line);
			}
		}


		private static IReadOnlyList<LinkLineOld> GetLines(
			IReadOnlyList<LinkLineOld> linkLines,
			IReadOnlyList<LinkSegmentOld> replacedSegments)
		{
			return replacedSegments
				.Where(segment => linkLines.Any(
					line => line.Source == segment.Source && line.Target == segment.Target))
				.Select(segment => linkLines.First(
					line => line.Source == segment.Source && line.Target == segment.Target))
				.ToList();
		}


		private bool AddDirectLine(LinkSegmentOld segment)
		{
			bool isNewAdded = false;
			LinkLineOld existingLine = segment.Owner.Links.OwnedLines
				.FirstOrDefault(line => line.Source == segment.Source && line.Target == segment.Target);

			if (existingLine == null)
			{
				existingLine = new LinkLineOld(this, segment.Source, segment.Target, segment.Owner);
				AddLinkLine(existingLine);

				segment.Owner.AddOwnedLineItem(existingLine);

				segment.Source.AncestorsAndSelf()
					.TakeWhile(node => node != segment.Owner)
					.ForEach(node => node.Links.TryAddReferencedLine(existingLine));
				segment.Target.AncestorsAndSelf()
					.TakeWhile(node => node != segment.Owner)
					.ForEach(node => node.Links.TryAddReferencedLine(existingLine));
				isNewAdded = true;
			}

			existingLine.TryAddLink(segment.Link);
			segment.Link.TryAddLinkLine(existingLine);

			return isNewAdded;
		}


		private void AddNormalLinkSegment(LinkSegmentOld segment)
		{
			LinkLineOld existingLine = segment.Owner.Links.OwnedLines
				.FirstOrDefault(line => line.Source == segment.Source && line.Target == segment.Target);

			if (existingLine == null)
			{
				existingLine = new LinkLineOld(this, segment.Source, segment.Target, segment.Owner);
				AddLinkLine(existingLine);
			}

			existingLine.IsNormal = true;

			existingLine.AddLink(segment.Link);
			segment.Link.TryAddLinkLine(existingLine);
		}


		private static void AddLinkLine(LinkLineOld line)
		{
			line.Owner.Links.TryAddOwnedLine(line);
			line.Source.Links.TryAddReferencedLine(line);
			line.Target.Links.TryAddReferencedLine(line);
		}


		public double GetLineThickness(LinkLineOld linkLine)
		{
			double scale = (linkLine.Owner.ItemsScale).MM(0.1, 0.7);
			double thickness;

			int linksCount = linkLine.Links.Count + linkLine.HiddenLinks.Count;

			if (linksCount < 5)
			{
				thickness = 1;
			}
			else if (linksCount < 15)
			{
				thickness = 2;
			}
			else
			{
				thickness = 3;
			}

			return thickness * scale;
		}


		public double GetArrowWidth(Line line)
		{
			double arrowWidth = (10 * line.ViewModel.ItemScale).MM(4, 15);

			if (line.ViewModel.IsMouseOver)
			{
				arrowWidth = (arrowWidth * 1.5).MM(0, 20);
			}

			return arrowWidth;
		}


		public LinkLineBounds GetLinkLineBounds(LinkLineOld line)
		{
			if (!IsNodesInitialized(line))
			{
				return LinkLineBounds.Empty;
			}

			(Point p1, Point p2) = GetLinkSegmentEndPoints(line);

			// Ensure the rect is at least big enough to contain the width of the actual line
			double margin = 2.5 / line.ItemsScale;

			Rect lineBounds = GetLineBounds(p1, p2, margin);

			(Point l1, Point l2) = GetLineEndPoints(p1, p2, margin);

			return new LinkLineBounds(lineBounds, l1, l2);
		}


		private static (Point l1, Point l2) GetLineEndPoints(Point p1, Point p2, double margin)
		{
			// Line drawing within the bounds
			double width = Math.Abs(p2.X - p1.X);
			double height = Math.Abs(p2.Y - p1.Y);


			if (p1.X <= p2.X && p1.Y <= p2.Y)
			{
				return (new Point(margin, margin), new Point(width, height));
			}
			else if (p1.X > p2.X && p1.Y <= p2.Y)
			{
				return (new Point(width, margin), new Point(margin, height));
			}
			else if (p1.X <= p2.X && p1.Y > p2.Y)
			{
				return (new Point(margin, height), new Point(width, margin));
			}
			else
			{
				return (new Point(width, height), new Point(margin, margin));
			}
		}


		private static Rect GetLineBounds(Point p1, Point p2, double margin)
		{
			// Line bounds:
			double x = Math.Min(p1.X, p2.X);
			double y = Math.Min(p1.Y, p2.Y);
			double width = Math.Abs(p2.X - p1.X);
			double height = Math.Abs(p2.Y - p1.Y);

			x = x - margin;
			y = y - margin;
			width = width + margin * 2;
			height = height + margin * 2;

			return new Rect(x, y, width, height);
		}


		private static (Point source, Point target) GetLinkSegmentEndPoints(LinkLineOld line)
		{
			NodeOld source = line.Source;
			NodeOld target = line.Target;
			Rect sourceBounds = source.ItemBounds;
			Rect targetBounds = target.ItemBounds;


			if (source.ParentNode == target.ParentNode)
			{
				// Source and target nodes are siblings, 
				// ie. line starts at source middle bottom and ends at target middle top
				double x1 = sourceBounds.X + sourceBounds.Width / 2;
				double y1 = sourceBounds.Y + sourceBounds.Height;
				Point sp = new Point(x1, y1);

				double x2 = targetBounds.X + targetBounds.Width / 2;
				double y2 = targetBounds.Y;
				Point tp = new Point(x2, y2);

				return (sp, tp);
			}
			else if (source.ParentNode == target)
			{
				// The target is a parent of the source,
				// i.e. line ends at the bottom of the target node
				double x1 = sourceBounds.X + sourceBounds.Width / 2;
				double y1 = sourceBounds.Y + sourceBounds.Height;
				Point sp = new Point(x1, y1);

				double x2 = targetBounds.X + targetBounds.Width / 2;
				double y2 = targetBounds.Y + targetBounds.Height;
				Point tp = ParentPointToChildPoint(target, new Point(x2, y2));

				return (sp, tp);
			}
			else if (source == target.ParentNode)
			{
				// The target is the child of the source,
				// i.e. line start at the top of the source
				double x1 = sourceBounds.X + sourceBounds.Width / 2;
				double y1 = sourceBounds.Y;
				Point sp = ParentPointToChildPoint(source, new Point(x1, y1));

				double x2 = targetBounds.X + targetBounds.Width / 2;
				double y2 = targetBounds.Y;
				Point tp = new Point(x2, y2);

				return (sp, tp);
			}
			else
			{
				// The line is between nodes, which are not within same node
				if (source == line.Owner)
				{
					double x1 = sourceBounds.X + sourceBounds.Width / 2;
					double y1 = sourceBounds.Y;
					Point sp = ParentPointToChildPoint(source, new Point(x1, y1));

					double x2 = targetBounds.X + targetBounds.Width / 2;
					double y2 = targetBounds.Y;
					Point tp = DescendentPointToAncestorPoint(target, line.Owner, new Point(x2, y2));

					return (sp, tp);
				}
				else if (target == line.Owner)
				{
					double x1 = sourceBounds.X + sourceBounds.Width / 2;
					double y1 = sourceBounds.Y + sourceBounds.Height;
					Point sp = DescendentPointToAncestorPoint(target, line.Owner, new Point(x1, y1));

					double x2 = targetBounds.X + targetBounds.Width / 2;
					double y2 = targetBounds.Y + targetBounds.Height;
					Point tp = ParentPointToChildPoint(target, new Point(x2, y2));

					return (sp, tp);
				}
				else
				{
					// Nodes are not direct siblings, need to use the common ancestor (owner)
					double x1 = sourceBounds.X + sourceBounds.Width / 2;
					double y1 = sourceBounds.Y + sourceBounds.Height;
					Point sp = DescendentPointToAncestorPoint(source, line.Owner, new Point(x1, y1));

					double x2 = targetBounds.X + targetBounds.Width / 2;
					double y2 = targetBounds.Y;
					Point tp = DescendentPointToAncestorPoint(target, line.Owner, new Point(x2, y2));

					return (sp, tp);
				}
			}
		}


		private static Point DescendentPointToAncestorPoint(NodeOld descendent, NodeOld ancestor, Point point)
		{
			foreach (NodeOld node in descendent.Ancestors())
			{
				if (node == ancestor)
				{
					break;
				}

				point = node.ChildCanvasPointToParentCanvasPoint(point);
			}

			return point;
		}


		private static Point ParentPointToChildPoint(NodeOld parent, Point point)
		{
			return parent.ParentCanvasPointToChildCanvasPoint(point);
		}


		private static bool IsNodesInitialized(LinkLineOld line) =>
			line.Source.ItemBounds != Rect.Empty && line.Target.ItemBounds != Rect.Empty;


		/// <summary>
		/// Gets the links in the line grouped first by source and then by target at the
		/// appropriate node levels.
		/// </summary>
		public IReadOnlyList<LinkGroup> GetLinkGroups(Line line)
		{
			Node source = line.Source;
			Node target = line.Target;
			IReadOnlyList<Link> links = line.Links;

			(int sourceLevel, int targetLevel) = GetNodeLevels(source, target);

			List<LinkGroup> linkGroups = new List<LinkGroup>();

			// Group links by grouping them based on node at source level
			var groupBySources = links.GroupBy(link => NodeAtLevel(link.Source, sourceLevel));
			foreach (var groupBySource in groupBySources)
			{
				// Sub-group these links by grouping them based on node at target level
				var groupByTargets = groupBySource.GroupBy(link => NodeAtLevel(link.Target, targetLevel));
				foreach (var groupByTarget in groupByTargets)
				{
					Node sourceNode = groupBySource.Key;
					Node targetNode = groupByTarget.Key;
					List<Link> groupLinks = groupByTarget.ToList();

					LinkGroup linkGroup = new LinkGroup(sourceNode, targetNode, groupLinks);
					linkGroups.Add(linkGroup);
				}
			}

			return linkGroups;
		}


		private static (int sourceLevel, int targetLevel) GetNodeLevels(Node source, Node target)
		{
			int sourceLevel = source.Ancestors().Count();
			int targetLevel = target.Ancestors().Count();

			if (source == target.Parent)
			{
				// Source node is parent of target
				targetLevel += 1;
			}
			//else if (source.Parent == target)
			//{
			//	// Source is child of target
			//	// sourceLevel += 1;
			//}
			//else
			//{
			//	// Siblings, dig into both source and level
			//	//sourceLevel += 1;
			//	//targetLevel += 1;
			//}

			return (sourceLevel, targetLevel);
		}


		private static Node NodeAtLevel(Node node, int level)
		{
			int count = 0;
			Node current = null;
			foreach (Node ancestor in node.AncestorsAndSelf().Reverse())
			{
				current = ancestor;
				if (count++ == level)
				{
					break;
				}
			}

			return current;
		}
	}
}