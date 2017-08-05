using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.Windows;
using Dependinator.ModelViewing.Nodes;

namespace Dependinator.ModelViewing.Links.Private
{
	internal class LineViewModelService : ILineViewModelService
	{
		private readonly ILinkSegmentService segmentService;

		private readonly Point middleBottom = new Point(0.5, 1);
		private readonly Point middleTop = new Point(0.5, 0);

		public LineViewModelService(ILinkSegmentService segmentService)
		{
			this.segmentService = segmentService;
		}


		public (Point source, Point target) GetLineEndPoints(
			Node sourceNode, Node targetNode, Point relativeSourceNode, Point relativeTargetNode)
		{
			Rect source = sourceNode.ViewModel.ItemBounds;
			Rect target = targetNode.ViewModel.ItemBounds;

			Point relativeSource = GetRelativeSource(sourceNode, targetNode, relativeSourceNode);
			Point relativeTarget = GetRelativeTarget(sourceNode, targetNode, relativeTargetNode);

			Point sp = source.Location + new Vector(
				source.Width * relativeSource.X, source.Height * relativeSource.Y);

			Point tp = target.Location + new Vector(
				target.Width * relativeTarget.X, target.Height * relativeTarget.Y);

			if (sourceNode.Parent == targetNode)
			{
				// Need to adjust for different scales
				tp = ParentPointToChildPoint(targetNode, tp);
			}
			else if (sourceNode == targetNode.Parent)
			{
				// Need to adjust for different scales
				sp = ParentPointToChildPoint(sourceNode, sp);
			}

			return (sp, tp);
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
		public IReadOnlyList<LinkGroup> GetLinkGroups(LinkLineOld line)
		{
			NodeOld source = line.Source;
			NodeOld target = line.Target;
			IReadOnlyList<LinkOld> links = line.Links;

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
					NodeOld sourceNode = groupBySource.Key;
					NodeOld targetNode = groupByTarget.Key;
					List<LinkOld> groupLinks = groupByTarget.ToList();

					LinkGroup linkGroup = new LinkGroup(sourceNode, targetNode, groupLinks);
					linkGroups.Add(linkGroup);
				}
			}

			return linkGroups;
		}


		private static (int sourceLevel, int targetLevel) GetNodeLevels(NodeOld source, NodeOld target)
		{
			int sourceLevel = source.Ancestors().Count();
			int targetLevel = target.Ancestors().Count();

			if (source == target.ParentNode)
			{
				// Source node is parent of target
				targetLevel += 1;
			}
			else if (source.ParentNode == target)
			{
				// Source is child of target
				sourceLevel += 1;
			}
			else
			{
				// Siblings, dig into both source and level
				sourceLevel += 1;
				targetLevel += 1;
			}

			return (sourceLevel, targetLevel);
		}


		private static NodeOld NodeAtLevel(NodeOld node, int level)
		{
			int count = 0;
			NodeOld current = null;
			foreach (NodeOld ancestor in node.AncestorsAndSelf().Reverse())
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