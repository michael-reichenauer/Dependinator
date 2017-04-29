using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using Dependiator.Modeling.Nodes;


namespace Dependiator.Modeling.Links
{
	internal class LinkItemService : ILinkItemService
	{
		public LinkSegmentLine GetLinkSegmentLine(LinkSegment segment)
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

			// Line bounds:
			double x = Math.Min(x1, x2);
			double y = Math.Min(y1, y2);
			double width = Math.Abs(x2 - x1);
			double height = Math.Abs(y2 - y1);

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

			if (x1 <= x2 && y1 > y2 || x1 > x2 && y1 <= y2)
			{
				// Need to flip the line
				ly1 = height - halfMargin;
				ly2 = halfMargin;
			}

			Point l1 = new Point(lx1, ly1);
			Point l2 = new Point(lx2, ly2);

			return new LinkSegmentLine(lineBounds, l1, l2);
		}


		public IReadOnlyList<LinkGroup> GetLinkGroups(LinkSegment segment)
		{
			Node source = segment.Source;
			Node target = segment.Target;
			IReadOnlyList<Link> links = segment.NodeLinks;

			int sourceLevel = source.Ancestors().Count();
			int targetLevel = target.Ancestors().Count();

			if (source == target.ParentNode)
			{
				// Source is parent of target
				targetLevel += 1;
			}
			else if (source.ParentNode == target)
			{
				// Source is child of target
				sourceLevel += 1;
			}
			else
			{
				// Siblings
				sourceLevel += 1;
				targetLevel += 1;
			}

			var groupBySources = links.GroupBy(l => NodeAtLevel(l.Source, sourceLevel));

			List<LinkGroup> linkGroups = new List<LinkGroup>();
			foreach (var sourceGroup in groupBySources)
			{
				var groupByTargets = sourceGroup.GroupBy(l => NodeAtLevel(l.Target, targetLevel));

				foreach (var targetGroup in groupByTargets)
				{
					linkGroups.Add(new LinkGroup(sourceGroup.Key, targetGroup.Key, targetGroup.ToList()));
				}
			}

			return linkGroups;
		}


		private static Node NodeAtLevel(Node node, int sourceLevel)
		{
			int count = 0;
			Node current = null;
			foreach (Node ancestor in node.AncestorsAndSelf().Reverse())
			{
				current = ancestor;
				if (count++ == sourceLevel)
				{
					break;
				}
			}

			return current;
		}
	}
}