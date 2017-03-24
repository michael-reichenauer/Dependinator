//using System.Collections.Generic;
//using System.Linq;
//using System.Windows;
//using Dependiator.Common.ThemeHandling;
//using Dependiator.MainViews;
//using Dependiator.MainViews.Private;
//using Dependiator.Utils;
//using Brush = System.Windows.Media.Brush;


using System.Linq;
using System.Windows;
using System.Windows.Media;
using Dependiator.Common.ThemeHandling;


namespace Dependiator.Modeling
{
	internal class NodeItemService : INodeItemService
	{
		private static readonly Size DefaultSize = new Size(200, 100);

		private readonly IThemeService themeService;


		public NodeItemService(
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


		public void SetInitalRootNodeBounds(Node node, Rect bounds)
		{
			node.ItemBounds = bounds;

			SetChildrenItemBounds(node);
		}



		private static void SetChildrenItemBounds(Node parent)
		{
			int rowLength = 6;

			int padding = 20;

			double xMargin = 10;
			double yMargin = 50;


			int count = 0;
			var children = parent.ChildNodes.OrderBy(child => child, NodeComparer.Comparer(parent));

			foreach (Node childNode in children)
			{
				Size size;
				Point location;

				if (childNode.NodeBounds.HasValue)
				{
					size = childNode.NodeBounds.Value.Size;
					location = childNode.NodeBounds.Value.Location;
				}
				else
				{
					size = DefaultSize;
					double x = (count % rowLength) * (size.Width + padding) + xMargin;
					double y = (count / rowLength) * (size.Height + padding) + yMargin;
					location = new Point(x, y);
				}

				Rect bounds = new Rect(location, size);
				childNode.ItemBounds = bounds;
				SetChildrenItemBounds(childNode);

				count++;
			}
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
//				MoveNode(node, canvasPoint, viewOffset, isFirst);
//			}

//			return node;
//		}

//		public bool ZoomItem(int zoomDelta, Point viewPosition)
//		{
//			return true;
//		}

//		private void MoveNode(Node node, Point canvasPoint, Vector viewOffset, bool isFirst)
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