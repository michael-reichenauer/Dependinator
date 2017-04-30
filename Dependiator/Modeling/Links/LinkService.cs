using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using Dependiator.Modeling.Nodes;


namespace Dependiator.Modeling.Links
{
	internal class LinkService : ILinkService
	{

		public IReadOnlyList<LinkLine> GetLinkSegments(Link link)
		{
			List<LinkLine> segments = new List<LinkLine>();

			// Start with first line at the start of the segmented line 
			Node segmentSource = link.Source;

			// Iterate segments until line end is reached
			while (segmentSource != link.Target)
			{
				// Try to assume next line target is a child node by searching if line source
				// is a ancestor of end target node
				Node segmentTarget = link.Target.AncestorsAndSelf()
					.FirstOrDefault(ancestor => ancestor.ParentNode == segmentSource);

				if (segmentTarget == null)
				{
					// Segment target was not a child, lets try to assume target is a sibling node
					segmentTarget = link.Target.AncestorsAndSelf()
						.FirstOrDefault(ancestor => ancestor.ParentNode == segmentSource.ParentNode);
				}

				if (segmentTarget == null)
				{
					// Segment target was neither child nor a sibling, next line target node must
					// be the parent node
					segmentTarget = segmentSource.ParentNode;
				}

				LinkLine line = GetSegment(segmentSource, segmentTarget, link);

				segments.Add(line);

				// Go to next line in the line segments 
				segmentSource = segmentTarget;
			}

			return segments;
		}


		private LinkLine GetSegment(Node source, Node target, Link link)
		{
			// The target is the child of the target, let the source own the line otherwise
			// the target is either a sibling or a parent of the source, let the source parent own.
			Node segmentOwner = source == target.ParentNode ? source : source.ParentNode;

			return new LinkLine(this, source, target, segmentOwner);
		}


		public double GetLineThickness(LinkLine linkLine)
		{
			double scale = (linkLine.Owner.ItemsScale).MM(0.1, 0.7);
			double thickness;

			if (linkLine.Links.Count < 5)
			{
				thickness = 1;
			}
			else if (linkLine.Links.Count < 15)
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
				y2 = (targetBounds.Height - 1) * target.ItemsScaleFactor
				     + (target.ItemsOffset.Y) / target.ItemsScale;
			}
			else if (source == target.ParentNode)
			{
				// The target is the child of the source, i.e. line start at the top of the source
				x1 = (sourceBounds.Width / 2) * source.ItemsScaleFactor
				     + source.ItemsOffset.X / source.ItemsScale;
				y1 = (source.ItemsOffset.Y + 1) / source.ItemsScale;
			}
			else if (source.ParentNode != target.ParentNode)
			{
				// Nodes are not direct siblings, need to use the common ancestor (owner)
				Point sp = GetPointInAncestorPoint(line, x1, y1, source);
				Point tp = GetPointInAncestorPoint(line, x2, y2, target);
				
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
				point = ancestor.GetChildToParentCanvasPoint(point);
				if (ancestor == line.Owner)
				{
					break;
				}
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