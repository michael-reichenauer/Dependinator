using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using Dependiator.Modeling.Nodes;


namespace Dependiator.Modeling.Links
{
	internal class LinkService : ILinkService
	{
		public double GetLineThickness(LinkSegment linkSegment)
		{
			double scale = (linkSegment.Owner.ItemsScale).MM(0.1, 0.7);
			double thickness;

			if (linkSegment.NodeLinks.Count < 5)
			{
				thickness = 1;
			}
			else if (linkSegment.NodeLinks.Count < 15)
			{
				thickness = 2;
			}
			else
			{
				thickness = 3;
			}

			return thickness * scale;
		}

		public LinkSegmentLine GetLinkSegmentLine(LinkSegment segment)
		{
			if (segment.Source.NodeBounds == Rect.Empty || segment.Target.NodeBounds == Rect.Empty)
			{
				return LinkSegmentLine.Empty;
			}

			(Point p1, Point p2) = GetLineEndPoints(segment);

			// Line bounds:
			double x = Math.Min(p1.X, p2.X);
			double y = Math.Min(p1.Y, p2.Y);
			double width = Math.Abs(p2.X - p1.X);
			double height = Math.Abs(p2.Y - p1.Y);

			// Ensure the rect is at least big enough to contain the width of the actual line
			double margin = 5 / segment.Owner.ItemsScale;
			double halfMargin = margin / 2;
			width = width + margin;
			height = height + margin;

			Rect lineBounds = new Rect(x - halfMargin, y - halfMargin, width, height);

			// Line drawing within the bounds
			double lx1 = halfMargin;
			double ly1 = halfMargin;
			double lx2 = width - halfMargin;
			double ly2 = height - halfMargin;

			if (p1.X <= p2.X && p1.Y > p2.Y || p1.X > p2.X && p1.Y <= p2.Y)
			{
				// Need to flip the line
				ly1 = height - halfMargin;
				ly2 = halfMargin;
			}

			Point l1 = new Point(lx1, ly1);
			Point l2 = new Point(lx2, ly2);

			return new LinkSegmentLine(lineBounds, l1, l2);
		}


		private static (Point source, Point target) GetLineEndPoints(LinkSegment segment)
		{
			Node source = segment.Source;
			Node target = segment.Target;
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
				Point sp = new Point(x1, y1);
				foreach (Node ancestor in source.Ancestors())
				{
					sp = ancestor.GetChildToParentCanvasPoint(sp);
					if (ancestor == segment.Owner)
					{
						break;
					}
				}

				Point tp = new Point(x2, y2);
				foreach (Node ancestor in target.Ancestors())
				{
					tp = ancestor.GetChildToParentCanvasPoint(tp);
					if (ancestor == segment.Owner)
					{
						break;
					}
				}

				x1 = sp.X;
				y1 = sp.Y;
				x2 = tp.X;
				y2 = tp.Y;
			}

			return (new Point(x1, y1), new Point(x2, y2));
		}


		/// <summary>
		/// Gets the links in the segment grouped first by source and then by target at the
		/// appropriate node levels.
		/// </summary>
		public IReadOnlyList<LinkGroup> GetLinkGroups(LinkSegment segment)
		{
			Node source = segment.Source;
			Node target = segment.Target;
			IReadOnlyList<Link> links = segment.NodeLinks;

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