using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using Dependinator.Modeling.Nodes;


namespace Dependinator.Modeling.Links
{
	internal class LinkService : ILinkService
	{
		private readonly ILinkSegmentService segmentService;


		public LinkService(ILinkSegmentService segmentService)
		{
			this.segmentService = segmentService;
		}


		public void AddLinkLines(Link link)
		{
			var linkSegments = segmentService.GetNormalLinkSegments(link);

			linkSegments.ForEach(AddNormalLinkSegment);
			link.SetLinkSegments(linkSegments);
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


		public void ZoomInLinkLine(LinkLine line, Node node)
		{
			IReadOnlyList<Link> links = line.Links.ToList();

			foreach (Link link in links)
			{
				IReadOnlyList<LinkSegment> currentLinkSegments = link.LinkSegments.ToList();

				IReadOnlyList<LinkSegment> zoomedSegments;
				if (node == line.Target)
				{
					zoomedSegments = segmentService.GetZoomedInBeforeReplacedSegments(currentLinkSegments, line.Source, line.Target);
				}
				else
				{
					zoomedSegments = segmentService.GetZoomedInAfterReplacedSegments(currentLinkSegments, line.Source, line.Target);
				}
			
				LinkSegment zoomedInSegment = segmentService.GetZoomedInSegment(zoomedSegments, link);

				var newSegments = segmentService.GetNewLinkSegments(currentLinkSegments, zoomedInSegment);

				var replacedLines = GetLines(link.Lines, zoomedSegments);

				replacedLines.ForEach(replacedLine => HideLinkFromLine(replacedLine, link));

				link.SetLinkSegments(newSegments);
				if (AddDirectLine(zoomedInSegment))
				{
					break;
				}
			}

			line.Owner.RootNode.UpdateNodeVisibility();
		}



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


		public void ZoomOutLinkLine(LinkLine line, Node node)
		{
			IReadOnlyList<Link> links = line.Links.ToList();

			foreach (Link link in links)
			{
				IReadOnlyList<LinkSegment> normalLinkSegments = segmentService.GetNormalLinkSegments(link);
				IReadOnlyList<LinkSegment> currentLinkSegments = link.LinkSegments.ToList();

				IReadOnlyList<LinkSegment> zoomedSegments = segmentService.GetZoomedOutReplacedSegments(normalLinkSegments, currentLinkSegments, line.Source, line.Target);

				if (zoomedSegments.Count > 1)
				{
					if (node == line.Target)
					{
						zoomedSegments = zoomedSegments.Skip(1).ToList();
					}
					else
					{
						zoomedSegments = zoomedSegments.Take(zoomedSegments.Count - 1).ToList();
					}

					LinkSegment zoomedInSegment = segmentService.GetZoomedInSegment(zoomedSegments, link);

					var newSegments = segmentService.GetNewLinkSegments(normalLinkSegments, zoomedInSegment);

					HideLinkFromLine(line, link);

					newSegments.ForEach(segment => AddDirectLine(segment));


					link.SetLinkSegments(newSegments);
				}		
			}

			line.Owner.RootNode.UpdateNodeVisibility();
		}


		public void CloseLine(LinkLine line)
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


		private void HideLinkFromLine(LinkLine line, Link link)
		{
			line.HideLink(link);
			link.Remove(line);
			
			if (!line.IsMouseOver && !line.IsNormal && !line.Links.Any())
			{
				CloseLine(line);
			}
		}


		private static IReadOnlyList<LinkLine> GetLines(
			IReadOnlyList<LinkLine> linkLines, 
			IReadOnlyList<LinkSegment> replacedSegments)
		{
			return replacedSegments
				.Where(segment => linkLines.Any(
					line => line.Source == segment.Source && line.Target == segment.Target))
				.Select(segment => linkLines.First(
					line => line.Source == segment.Source && line.Target == segment.Target))
				.ToList();
		}


		private bool AddDirectLine(LinkSegment segment)
		{
			bool isNewAdded = false;
			LinkLine existingLine = segment.Owner.Links.OwnedLines
				.FirstOrDefault(line => line.Source == segment.Source && line.Target == segment.Target);

			if (existingLine == null)
			{
				existingLine = new LinkLine(this, segment.Source, segment.Target, segment.Owner);
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


		private void AddNormalLinkSegment(LinkSegment segment)
		{
			LinkLine existingLine = segment.Owner.Links.OwnedLines
				.FirstOrDefault(line => line.Source == segment.Source && line.Target == segment.Target);

			if (existingLine == null)
			{
				existingLine = new LinkLine(this, segment.Source, segment.Target, segment.Owner);
				AddLinkLine(existingLine);
			}

			existingLine.IsNormal = true;

			existingLine.AddLink(segment.Link);
			segment.Link.TryAddLinkLine(existingLine);
		}


		private static void AddLinkLine(LinkLine line)
		{
			line.Owner.Links.TryAddOwnedLine(line);
			line.Source.Links.TryAddReferencedLine(line);
			line.Target.Links.TryAddReferencedLine(line);
		}



		public double GetLineThickness(LinkLine linkLine)
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

		public LinkLineBounds GetLinkLineBounds(LinkLine line)
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


			if (p1.X <= p2.X && p1.Y <= p2.Y )
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


		private static (Point source, Point target) GetLinkSegmentEndPoints(LinkLine line)
		{
			Node source = line.Source;
			Node target = line.Target;
			Rect sourceBounds = source.NodeBounds;
			Rect targetBounds = target.NodeBounds;

			// We start by assuming source and target nodes are siblings, 
			// I.e. line starts at source middle bottom and ends at target middle top
			double x1 = sourceBounds.X + sourceBounds.Width / 2;
			double y1 = sourceBounds.Y + sourceBounds.Height;
			double x2 = targetBounds.X + targetBounds.Width / 2;
			double y2 = targetBounds.Y;

			if (source.ParentNode == target)
			{
				// The target is a parent of the source, i.e. line ends at the bottom of the target node
				x2 = (targetBounds.Width / 2) * target.ItemsScaleFactor
				     + target.ItemsOffset.X / target.ItemsScale;
				y2 = (targetBounds.Height) * target.ItemsScaleFactor
				     + (target.ItemsOffset.Y) / target.ItemsScale;
			}
			else if (source == target.ParentNode)
			{
				// The target is the child of the source, i.e. line start at the top of the source
				x1 = (sourceBounds.Width / 2) * source.ItemsScaleFactor
				     + source.ItemsOffset.X / source.ItemsScale;
				y1 = (source.ItemsOffset.Y) / source.ItemsScale;
			}
			else if (source.ParentNode != target.ParentNode)
			{
				Point sp;
				Point tp;
				if (source == line.Owner)
				{
					x1 = (sourceBounds.Width / 2) * source.ItemsScaleFactor
					     + source.ItemsOffset.X / source.ItemsScale;
					y1 = (source.ItemsOffset.Y) / source.ItemsScale;
					sp = new Point(x1, y1);
					tp = GetPointInAncestorPoint(line, x2, y2, target);
				}
				else if (target == line.Owner)
				{
					x2 = (targetBounds.Width / 2) * target.ItemsScaleFactor
					     + target.ItemsOffset.X / target.ItemsScale;
					y2 = (targetBounds.Height) * target.ItemsScaleFactor
					     + (target.ItemsOffset.Y) / target.ItemsScale;
					tp = new Point(x2, y2);
					sp = GetPointInAncestorPoint(line, x1, y1, target);
				}
				else
				{
					// Nodes are not direct siblings, need to use the common ancestor (owner)
					sp = GetPointInAncestorPoint(line, x1, y1, source);
					tp = GetPointInAncestorPoint(line, x2, y2, target);
				}
				
				
				x1 = sp.X;
				y1 = sp.Y;
				x2 = tp.X;
				y2 = tp.Y;
			}

			return (new Point(x1, y1), new Point(x2, y2));
		}


		private static Point GetPointInAncestorPoint(LinkLine line, double x, double y, Node node)
		{
			Point point = new Point(x, y);

			foreach (Node ancestor in node.Ancestors())
			{
				if (ancestor == line.Owner)
				{
					break;
				}

				point = ancestor.GetChildToParentCanvasPoint(point);				
			}
			
			return point;
		}


		private static bool IsNodesInitialized(LinkLine line) =>
			line.Source.NodeBounds != Rect.Empty && line.Target.NodeBounds != Rect.Empty;


		/// <summary>
		/// Gets the links in the line grouped first by source and then by target at the
		/// appropriate node levels.
		/// </summary>
		public IReadOnlyList<LinkGroup> GetLinkGroups(LinkLine line)
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