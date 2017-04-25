using System;
using System.Linq;
using System.Windows;
using System.Windows.Forms;
using System.Windows.Media;
using Dependiator.Common.ThemeHandling;
using Dependiator.Modeling.Links;
using Dependiator.Modeling.Nodes;


namespace Dependiator.Modeling
{
	internal class ItemService : IItemService
	{
		private static readonly Size DefaultSize = new Size(200, 100);

		private readonly IThemeService themeService;


		public ItemService(
			IThemeService themeService)
		{
			this.themeService = themeService;
		}


		public Brush GetRandomRectangleBrush()
		{
			return themeService.GetRectangleBrush();
		}

		public Brush GetBrushFromHex(string hexColor)
		{
			return themeService.GetBrushFromHex(hexColor);
		}

		public string GetHexColorFromBrush(Brush brush)
		{
			return themeService.GetHexColorFromBrush(brush);
		}

		public Brush GetRectangleBackgroundBrush(Brush brush)
		{
			return themeService.GetRectangleBackgroundBrush(brush);
		}


		public void SetChildrenLayout(Node parent)
		{
			int rowLength = 6;

			int padding = 20;

			double xMargin = 10;
			double yMargin = 100;

			int count = 0;
			var children = parent.ChildNodes.OrderBy(child => child, NodeComparer.Comparer(parent));

			foreach (Node childNode in children)
			{
				Size size;
				Point location;

				if (childNode.PersistentNodeBounds.HasValue)
				{
					size = childNode.PersistentNodeBounds.Value.Size;
					location = childNode.PersistentNodeBounds.Value.Location;
				}
				else
				{
					size = DefaultSize;
					double x = (count % rowLength) * (size.Width + padding) + xMargin;
					double y = (count / rowLength) * (size.Height + padding) + yMargin;
					location = new Point(x, y);
				}

				Rect bounds = new Rect(location, size);
				childNode.NodeBounds = bounds;
				count++;
			}
		}

		public void UpdateLine(LinkSegment segment)
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

			// Ensure the rect is at least big enough to contain the width of the line
			double margin = 5 / segment.Owner.ItemsScale;
			double hm = margin / 2;
			width = width + margin;
			height = height + margin;

			Rect lineBounds = new Rect(x - hm, y - hm, width, height);

			// Line drawing within the bounds
			double lx1 = hm;
			double ly1 = hm;
			double lx2 = width - hm;
			double ly2 = height - hm;

			if (x1 <= x2 && y1 > y2 || x1 > x2 && y1 <= y2)
			{
				// Need to flip the line
				ly1 = height - hm;
				ly2 = hm;
			}

			Point l1 = new Point(lx1, ly1);
			Point l2 = new Point(lx2, ly2);

			segment.SetBounds(lineBounds, l1, l2);
		}
	}
}
