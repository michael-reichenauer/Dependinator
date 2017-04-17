//using System.Collections.Generic;
//using System.Linq;
//using System.Windows;
//using Dependiator.Common.ThemeHandling;
//using Dependiator.MainViews;
//using Dependiator.MainViews.Private;
//using Dependiator.Utils;
//using Brush = System.Windows.Media.Brush;


using System;
using System.Linq;
using System.Windows;
using System.Windows.Media;
using Dependiator.Common.ThemeHandling;


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
				y2 = (targetBounds.Height) * target.ItemsScaleFactor
				     + (target.ItemsOffset.Y) / target.ItemsScale;

			}
			else if (source == target.ParentNode)
			{
				// The target is the child of the source, i.e. line start at the top of the source
				x1 = (sourceBounds.Width / 2) * source.ItemsScaleFactor
				     + source.ItemsOffset.X / source.ItemsScale;
				y1 = source.ItemsOffset.Y / source.ItemsScale;
			}

			// Line bounds:
			double x = Math.Min(x1, x2);
			double y = Math.Min(y1, y2);
			double width = Math.Abs(x2 - x1);
			double height = Math.Abs(y2 - y1);

			// Ensure the rect is at least big enough to contain the width of the line
			width = Math.Max(width, segment.LineThickness + 1);
			height = Math.Max(height, segment.LineThickness + 1);

			Rect lineBounds = new Rect(x, y, width, height);

			// Line drawing within the bounds
			double lx1 = 0;
			double ly1 = 0;
			double lx2 = width;
			double ly2 = height;

			if (x1 <= x2 && y1 > y2 || x1 > x2 && y1 <= y2)
			{
				// Need to flip the line
				ly1 = height;
				ly2 = 0;
			}

			Point l1 = new Point(lx1, ly1);
			Point l2 = new Point(lx2, ly2);

			segment.SetBounds(lineBounds, l1, l2);
		}
	}
}


//	[SingleInstance]
//	internal class ItemService : IItemService
//	{
//		private readonly ICanvasService canvasService;
//		private readonly INodeItemsSource itemsSource;
//		private readonly IThemeService themeService;

//		private readonly List<Item> rootNodes = new List<Item>();

//		public ItemService(
//			ICanvasService canvasService,
//			INodeItemsSource itemsSource,
//			IThemeService themeService)
//		{
//			this.canvasService = canvasService;
//			this.itemsSource = itemsSource;
//			this.themeService = themeService;

//			canvasService.ScaleChanged += (s, e) => OnScaleChanged();
//		}


//		public double CanvasScale
//		{
//			get { return canvasService.Scale; }
//			set { canvasService.Scale = value; }			
//		} 

//		public Rect CurrentViewPort => canvasService.CurrentViewPort;

//		public Point Offset => canvasService.Offset;


//		public void ShowRootItem(Item item)
//		{
//			item.ItemRealized();
//		}

//		public void ClearAll()
//		{
//			itemsSource.Clear();
//		}

//		public void ShowItems(IEnumerable<Item> nodes)
//		{
//			itemsSource.Add(nodes);
//		}


//		public void HideItems(IEnumerable<Item> nodes)
//		{
//			itemsSource.Remove(nodes);
//		}


//		public void ShowItem(Item item)
//		{
//			itemsSource.Add(item);
//		}


//		public void HideItem(Item item)
//		{
//			itemsSource.Remove(item);
//		}





//		public void UpdateItem(Item item)
//		{
//			itemsSource.Update(item);
//		}


//		public Brush GetRectangleBackgroundBrush(Brush brush)
//		{
//			return themeService.GetRectangleBackgroundBrush(brush);
//		}


//		public Brush GetRectangleBrush()
//		{
//			return themeService.GetRectangleBrush();
//		}


//		public void AddRootItem(Item item)
//		{
//			rootNodes.Add(item);
//		}


//		public object MoveItem(Point viewPosition, Vector viewOffset, object movingObject)
//		{
//			Node node = movingObject as Node;

//			Point canvasPoint = canvasService.GetCanvasPoint(viewPosition);
//			bool isFirst = false;

//			if (node == null)
//			{
//				Point point = new Point(canvasPoint.X - 6 / CanvasScale, canvasPoint.Y - 6 / CanvasScale);
//				Rect area = new Rect(point, new Size(6 / CanvasScale, 6 / CanvasScale));

//				node = itemsSource
//					.GetItemsInArea(area)
//					.OfType<Node>()
//					.LastOrDefault(item => item.ParentItem != null);
//				isFirst = true;
//			}

//			if (node != null)
//			{
//				Move(node, canvasPoint, viewOffset, isFirst);
//			}

//			return node;
//		}

//		public bool ZoomItem(int zoomDelta, Point viewPosition)
//		{
//			return true;
//		}

//		private void Move(Node node, Point canvasPoint, Vector viewOffset, bool isFirst)
//		{
//			//module.Move(viewOffset);
//			node.MoveOrResize(canvasPoint, viewOffset, isFirst);
//		}

//		public void RemoveRootNode(Item item)
//		{
//			rootNodes.Remove(item);
//		}


//		private void OnScaleChanged()
//		{
//			rootNodes.ToList().ForEach(node => node.ChangedScale());
//		}
//	}
//}