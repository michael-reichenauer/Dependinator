using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using Dependinator.ModelViewing.ModelHandling.Core;
using Dependinator.ModelViewing.Nodes;
using Dependinator.Utils.UI;

namespace Dependinator.ModelViewing.Links.Private
{
	internal class LineViewModelService : ILineViewModelService
	{
		private static readonly double LineMargin = 10;

		private readonly IGeometryService geometryService;
		private readonly ILinkMenuItemService linkMenuItemService;

		private readonly Point middleBottom = new Point(0.5, 1);
		private readonly Point middleTop = new Point(0.5, 0);

		public LineViewModelService(
			IGeometryService geometryService,
			ILinkMenuItemService linkMenuItemService)
		{
			this.geometryService = geometryService;
			this.linkMenuItemService = linkMenuItemService;
		}



		public IEnumerable<LinkItem> GetSourceLinkItems(Line line)
		{
			IEnumerable<LinkItem> items = linkMenuItemService.GetSourceLinkItems(line);
			return items;
		}


		public IEnumerable<LinkItem> GetTargetLinkItems(Line line)
		{
			IEnumerable<LinkItem> items = linkMenuItemService.GetTargetLinkItems(line);
			return items;
		}


		public void UpdateLineBounds(Line line)
		{
			Rect bounds = new Rect(line.FirstPoint, line.LastPoint);

			// Adjust boundaries for line points between first and last point
			line.MiddlePoints().ForEach(point => bounds.Union(point));

			// The items bound needs some margin around the line to allow line width and arrow to show
			double margin = LineMargin / line.ViewModel.ItemScale;
			bounds.Inflate(margin, margin);

			// Set the new bounds
			line.ViewModel.ItemBounds = bounds;
		}


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
				// Need to adjust for different scales
				Vector vector = line.ViewModel.ItemScale > 1
					? new Vector(0, 8 / line.ViewModel.ItemScale) : new Vector(0, 8);

				tp = ParentPointToChildPoint(line.Target, tp) - vector;
			}
			else if (line.Source == line.Target.Parent)
			{
				// Need to adjust for different scales
				sp = ParentPointToChildPoint(line.Source, sp);
			}

			line.FirstPoint = sp;
			line.LastPoint = tp;
		}


		public bool IsOnLineBetweenNeighbors(Line line, int index)
		{
			Point p = line.Points[index];
			Point a = line.Points[index - 1];
			Point b = line.Points[index + 1];

			double length = geometryService.GetDistanceFromLine(a, b, p);
			return length < 0.1;

		}


		public void MoveLinePoint(Line line, int pointIndex, Point newPoint)
		{
			// NOTE: These lines are currently disabled !!!
			NodeViewModel source = line.Source.View.ViewModel;
			NodeViewModel target = line.Target.View.ViewModel;

			if (pointIndex == line.FirstIndex)
			{
				// Adjust point to be on the source node perimeter
				newPoint = geometryService.GetPointInPerimeter(source.ItemBounds, newPoint);
				line.RelativeSourcePoint = new Point(
					(newPoint.X - source.ItemBounds.X) / source.ItemBounds.Width,
					(newPoint.Y - source.ItemBounds.Y) / source.ItemBounds.Height);
			}
			else if (pointIndex == line.LastIndex)
			{
				// Adjust point to be on the target node perimeter
				newPoint = geometryService.GetPointInPerimeter(target.ItemBounds, newPoint);
				line.RelativeTargetPoint = new Point(
					(newPoint.X - target.ItemBounds.X) / target.ItemBounds.Width,
					(newPoint.Y - target.ItemBounds.Y) / target.ItemBounds.Height);
			}
			else
			{
				Point a = line.Points[pointIndex - 1];
				Point b = line.Points[pointIndex + 1];
				Point p = newPoint;
				if (geometryService.GetDistanceFromLine(a, b, p) < 0.1)
				{
					newPoint = geometryService.GetClosestPointOnLineSegment(a, b, p);
				}
			}

			line.Points[pointIndex] = newPoint;
		}



		public int GetLinePointIndex(Line line, Point point, bool isPointMove)
		{
			IList<Point> points = line.Points;
			double itemScale = line.ViewModel.ItemScale;

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


		private Point GetRelativeSource(Line line)
		{
			if (line.RelativeSourcePoint.X >= 0)
			{
				// use specified source
				return line.RelativeSourcePoint;
			}

			if (line.Source == line.Target.Parent)
			{
				// The target is the child of the source,
				// i.e. line start at the top of the source and goes to target top
				return middleTop;
			}

			// If target is sibling or parent
			// i.e. line start at the bottom of the source and goes to target top
			return middleBottom;
		}



		private Point GetRelativeTarget(Line line)
		{
			if (line.RelativeTargetPoint.X >= 0)
			{
				// use specified source
				return line.RelativeTargetPoint;
			}

			if (line.Source.Parent == line.Target)
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
			double d = GetLineWidth(line).MM(2, 4);

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

			if (line.ViewModel.IsMouseOver)
			{
				lineLineWidth = (lineLineWidth * 1.5).MM(0, 6);
			}

			return lineLineWidth;
		}


		public string GetLineToolTip(Line line)
			//=> $"{line.Links.Count} links";
			=> $"{line.Source.Name.DisplayFullName} -> {line.Target.Name.DisplayFullName}, {line.Links.Count} links";


		private static Point ParentPointToChildPoint(Node parent, Point point)
		{
			return parent.View.ItemsCanvas.ParentToChildCanvasPoint(point);
		}


		//public void AddLinkLines(LinkOld link)
		//{
		//	//var linkSegments = segmentService.GetNormalLinkSegments(link);

		//	//linkSegments.ForEach(AddNormalLinkSegment);
		//	//link.SetLinkSegments(linkSegments);
		//}


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


		//public void ZoomInLinkLine(LinkLineOld line, NodeOld node)
		//{
		//	//	IReadOnlyList<LinkOld> links = line.Links.ToList();

		//	//	foreach (LinkOld link in links)
		//	//	{
		//	//		IReadOnlyList<LinkSegmentOld> currentLinkSegments = link.LinkSegments.ToList();

		//	//		IReadOnlyList<LinkSegmentOld> zoomedSegments;
		//	//		if (node == line.Target)
		//	//		{
		//	//			zoomedSegments = segmentService.GetZoomedInBeforeReplacedSegments(currentLinkSegments, line.Source, line.Target);
		//	//		}
		//	//		else
		//	//		{
		//	//			zoomedSegments = segmentService.GetZoomedInAfterReplacedSegments(currentLinkSegments, line.Source, line.Target);
		//	//		}

		//	//		LinkSegmentOld zoomedInSegment = segmentService.GetZoomedInSegment(zoomedSegments, link);

		//	//		var newSegments = segmentService.GetNewLinkSegments(currentLinkSegments, zoomedInSegment);

		//	//		var replacedLines = GetLines(link.Lines, zoomedSegments);

		//	//		replacedLines.ForEach(replacedLine => HideLinkFromLine(replacedLine, link));

		//	//		link.SetLinkSegments(newSegments);
		//	//		if (AddDirectLine(zoomedInSegment))
		//	//		{
		//	//			break;
		//	//		}
		//	//	}

		//	//	line.Owner.RootNode.UpdateNodeVisibility();
		//	//}


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
		//}


		//public void ZoomOutLinkLine(LinkLineOld line, NodeOld node)
		//{
		//	//IReadOnlyList<LinkOld> links = line.Links.ToList();

		//	//foreach (LinkOld link in links)
		//	//{
		//	//	IReadOnlyList<LinkSegmentOld> normalLinkSegments = segmentService.GetNormalLinkSegments(link);
		//	//	IReadOnlyList<LinkSegmentOld> currentLinkSegments = link.LinkSegments.ToList();

		//	//	IReadOnlyList<LinkSegmentOld> zoomedSegments = segmentService.GetZoomedOutReplacedSegments(normalLinkSegments, currentLinkSegments, line.Source, line.Target);

		//	//	if (zoomedSegments.Count > 1)
		//	//	{
		//	//		if (node == line.Target)
		//	//		{
		//	//			zoomedSegments = zoomedSegments.Skip(1).ToList();
		//	//		}
		//	//		else
		//	//		{
		//	//			zoomedSegments = zoomedSegments.Take(zoomedSegments.Count - 1).ToList();
		//	//		}

		//	//		LinkSegmentOld zoomedInSegment = segmentService.GetZoomedInSegment(zoomedSegments, link);

		//	//		var newSegments = segmentService.GetNewLinkSegments(normalLinkSegments, zoomedInSegment);

		//	//		HideLinkFromLine(line, link);

		//	//		newSegments.ForEach(segment => AddDirectLine(segment));


		//	//		link.SetLinkSegments(newSegments);
		//	//	}		
		//	//}

		//	//line.Owner.RootNode.UpdateNodeVisibility();
		//}


		//public void CloseLine(LinkLineOld line)
		//{
		//	line.Owner.RemoveOwnedLineItem(line);
		//	line.Owner.Links.RemoveOwnedLine(line);

		//	line.Source.Links.RemoveReferencedLine(line);
		//	line.Target.Links.RemoveReferencedLine(line);

		//	line.Source.AncestorsAndSelf()
		//		.TakeWhile(node => node != line.Owner)
		//		.ForEach(node => node.Links.RemoveReferencedLine(line));

		//	line.Target.AncestorsAndSelf()
		//		.TakeWhile(node => node != line.Owner)
		//		.ForEach(node => node.Links.RemoveReferencedLine(line));
		//}


		//private void HideLinkFromLine(LinkLineOld line, LinkOld link)
		//{
		//	line.HideLink(link);
		//	link.Remove(line);

		//	if (!line.IsMouseOver && !line.IsNormal && !line.Links.Any())
		//	{
		//		CloseLine(line);
		//	}
		//}


		//private static IReadOnlyList<LinkLineOld> GetLines(
		//	IReadOnlyList<LinkLineOld> linkLines,
		//	IReadOnlyList<LinkSegmentOld> replacedSegments)
		//{
		//	return replacedSegments
		//		.Where(segment => linkLines.Any(
		//			line => line.Source == segment.Source && line.Target == segment.Target))
		//		.Select(segment => linkLines.First(
		//			line => line.Source == segment.Source && line.Target == segment.Target))
		//		.ToList();
		//}


		//private bool AddDirectLine(LinkSegmentOld segment)
		//{
		//	bool isNewAdded = false;
		//	LinkLineOld existingLine = segment.Owner.Links.OwnedLines
		//		.FirstOrDefault(line => line.Source == segment.Source && line.Target == segment.Target);

		//	if (existingLine == null)
		//	{
		//		existingLine = new LinkLineOld(this, segment.Source, segment.Target, segment.Owner);
		//		AddLinkLine(existingLine);

		//		segment.Owner.AddOwnedLineItem(existingLine);

		//		segment.Source.AncestorsAndSelf()
		//			.TakeWhile(node => node != segment.Owner)
		//			.ForEach(node => node.Links.TryAddReferencedLine(existingLine));
		//		segment.Target.AncestorsAndSelf()
		//			.TakeWhile(node => node != segment.Owner)
		//			.ForEach(node => node.Links.TryAddReferencedLine(existingLine));
		//		isNewAdded = true;
		//	}

		//	existingLine.TryAddLink(segment.Link);
		//	segment.Link.TryAddLinkLine(existingLine);

		//	return isNewAdded;
		//}


		//private void AddNormalLinkSegment(LinkSegmentOld segment)
		//{
		//	LinkLineOld existingLine = segment.Owner.Links.OwnedLines
		//		.FirstOrDefault(line => line.Source == segment.Source && line.Target == segment.Target);

		//	if (existingLine == null)
		//	{
		//		existingLine = new LinkLineOld(this, segment.Source, segment.Target, segment.Owner);
		//		AddLinkLine(existingLine);
		//	}

		//	existingLine.IsNormal = true;

		//	existingLine.AddLink(segment.Link);
		//	segment.Link.TryAddLinkLine(existingLine);
		//}


		//private static void AddLinkLine(LinkLineOld line)
		//{
		//	line.Owner.Links.TryAddOwnedLine(line);
		//	line.Source.Links.TryAddReferencedLine(line);
		//	line.Target.Links.TryAddReferencedLine(line);
		//}


		//public double GetLineThickness(LinkLineOld linkLine)
		//{
		//	double scale = (linkLine.Owner.ItemsScale).MM(0.1, 0.7);
		//	double thickness;

		//	int linksCount = linkLine.Links.Count + linkLine.HiddenLinks.Count;

		//	if (linksCount < 5)
		//	{
		//		thickness = 1;
		//	}
		//	else if (linksCount < 15)
		//	{
		//		thickness = 2;
		//	}
		//	else
		//	{
		//		thickness = 3;
		//	}

		//	return thickness * scale;
		//}


		public double GetArrowWidth(Line line)
		{
			double arrowWidth = (10 * line.ViewModel.ItemScale).MM(6, 15);

			if (line.ViewModel.IsMouseOver)
			{
				arrowWidth = (arrowWidth * 1.5).MM(0, 20);
			}

			return arrowWidth;
		}



		//public LinkLineBounds GetLinkLineBounds(LinkLineOld line)
		//{
		//	if (!IsNodesInitialized(line))
		//	{
		//		return LinkLineBounds.Empty;
		//	}

		//	(Point p1, Point p2) = GetLinkSegmentEndPoints(line);

		//	// Ensure the rect is at least big enough to contain the width of the actual line
		//	double margin = 2.5 / line.ItemsScale;

		//	Rect lineBounds = GetLineBounds(p1, p2, margin);

		//	(Point l1, Point l2) = GetLineEndPoints(p1, p2, margin);

		//	return new LinkLineBounds(lineBounds, l1, l2);
		//}


		//private static (Point l1, Point l2) GetLineEndPoints(Point p1, Point p2, double margin)
		//{
		//	// Line drawing within the bounds
		//	double width = Math.Abs(p2.X - p1.X);
		//	double height = Math.Abs(p2.Y - p1.Y);


		//	if (p1.X <= p2.X && p1.Y <= p2.Y)
		//	{
		//		return (new Point(margin, margin), new Point(width, height));
		//	}
		//	else if (p1.X > p2.X && p1.Y <= p2.Y)
		//	{
		//		return (new Point(width, margin), new Point(margin, height));
		//	}
		//	else if (p1.X <= p2.X && p1.Y > p2.Y)
		//	{
		//		return (new Point(margin, height), new Point(width, margin));
		//	}
		//	else
		//	{
		//		return (new Point(width, height), new Point(margin, margin));
		//	}
		//}


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


		//private static (Point source, Point target) GetLinkSegmentEndPoints(LinkLineOld line)
		//{
		//	NodeOld source = line.Source;
		//	NodeOld target = line.Target;
		//	Rect sourceBounds = source.ItemBounds;
		//	Rect targetBounds = target.ItemBounds;


		//	if (source.ParentNode == target.ParentNode)
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
		//	else if (source.ParentNode == target)
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
		//	else if (source == target.ParentNode)
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
		//	else
		//	{
		//		// The line is between nodes, which are not within same node
		//		if (source == line.Owner)
		//		{
		//			double x1 = sourceBounds.X + sourceBounds.Width / 2;
		//			double y1 = sourceBounds.Y;
		//			Point sp = ParentPointToChildPoint(source, new Point(x1, y1));

		//			double x2 = targetBounds.X + targetBounds.Width / 2;
		//			double y2 = targetBounds.Y;
		//			Point tp = DescendentPointToAncestorPoint(target, line.Owner, new Point(x2, y2));

		//			return (sp, tp);
		//		}
		//		else if (target == line.Owner)
		//		{
		//			double x1 = sourceBounds.X + sourceBounds.Width / 2;
		//			double y1 = sourceBounds.Y + sourceBounds.Height;
		//			Point sp = DescendentPointToAncestorPoint(target, line.Owner, new Point(x1, y1));

		//			double x2 = targetBounds.X + targetBounds.Width / 2;
		//			double y2 = targetBounds.Y + targetBounds.Height;
		//			Point tp = ParentPointToChildPoint(target, new Point(x2, y2));

		//			return (sp, tp);
		//		}
		//		else
		//		{
		//			// Nodes are not direct siblings, need to use the common ancestor (owner)
		//			double x1 = sourceBounds.X + sourceBounds.Width / 2;
		//			double y1 = sourceBounds.Y + sourceBounds.Height;
		//			Point sp = DescendentPointToAncestorPoint(source, line.Owner, new Point(x1, y1));

		//			double x2 = targetBounds.X + targetBounds.Width / 2;
		//			double y2 = targetBounds.Y;
		//			Point tp = DescendentPointToAncestorPoint(target, line.Owner, new Point(x2, y2));

		//			return (sp, tp);
		//		}
		//	}
		//}


		//private static Point DescendentPointToAncestorPoint(NodeOld descendent, NodeOld ancestor, Point point)
		//{
		//	foreach (NodeOld node in descendent.Ancestors())
		//	{
		//		if (node == ancestor)
		//		{
		//			break;
		//		}

		//		point = node.ChildCanvasPointToParentCanvasPoint(point);
		//	}

		//	return point;
		//}


		//private static Point ParentPointToChildPoint(NodeOld parent, Point point)
		//{
		//	return parent.ParentCanvasPointToChildCanvasPoint(point);
		//}


		//private static bool IsNodesInitialized(LinkLineOld line) =>
		//	line.Source.ItemBounds != Rect.Empty && line.Target.ItemBounds != Rect.Empty;



		/// <summary>
		/// Gets the links in the line grouped first by source and then by target at the
		/// appropriate node levels.
		/// </summary>
		public IReadOnlyList<LinkGroup> GetLinkGroups2(Line line)
		{
			Node source = line.Source;
			Node target = line.Target;
			IReadOnlyList<Link> links = line.Links;

			List<LinkGroup> linkGroups = new List<LinkGroup>();

			var groupByTargets = links.GroupBy(link => link.Target);
			foreach (IGrouping<Node, Link> groupByTarget in groupByTargets)
			{
				var groupBySourceParents = groupByTarget.GroupBy(link => link.Source.Parent);

				foreach (var groupBySourceParent in groupBySourceParents)
				{
					LinkGroup linkGroup = new LinkGroup(
						groupBySourceParent.Key, groupByTarget.Key, groupBySourceParent.ToList());

					linkGroups.Add(linkGroup);
				}
			}

			return linkGroups;
		}


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

			// RootGroup links by grouping them based on node at source level
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