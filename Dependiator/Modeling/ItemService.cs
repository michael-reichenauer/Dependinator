using System.Collections.Generic;
using System.Linq;
using System.Windows;
using Dependiator.Common.ThemeHandling;
using Dependiator.MainViews;
using Dependiator.MainViews.Private;
using Dependiator.Utils;
using Brush = System.Windows.Media.Brush;


namespace Dependiator.Modeling
{
	[SingleInstance]
	internal class ItemService : IItemService
	{
		private readonly ICanvasService canvasService;
		private readonly IMainViewItemsSource itemsSource;
		private readonly IThemeService themeService;

		private readonly List<Item> rootNodes = new List<Item>();

		public ItemService(
			ICanvasService canvasService,
			IMainViewItemsSource itemsSource,
			IThemeService themeService)
		{
			this.canvasService = canvasService;
			this.itemsSource = itemsSource;
			this.themeService = themeService;

			canvasService.ScaleChanged += (s, e) => OnScaleChanged();
		}


		public double CanvasScale
		{
			get { return canvasService.Scale; }
			set { canvasService.Scale = value; }			
		} 

		public Rect CurrentViewPort => canvasService.CurrentViewPort;

		public Point Offset => canvasService.Offset;


		public void ShowRootItem(Item item)
		{
			item.ItemRealized();
		}

		public void ClearAll()
		{
			itemsSource.Clear();
		}

		public void ShowItems(IEnumerable<Item> nodes)
		{
			itemsSource.Add(nodes);
		}


		public void HideItems(IEnumerable<Item> nodes)
		{
			itemsSource.Remove(nodes);
		}


		public void ShowItem(Item item)
		{
			itemsSource.Add(item);
		}


		public void HideItem(Item item)
		{
			itemsSource.Remove(item);
		}

		public void UpdateItem(Item item)
		{
			itemsSource.Update(item);
		}


		public Brush GetRectangleBackgroundBrush(Brush brush)
		{
			return themeService.GetRectangleBackgroundBrush(brush);
		}


		public Brush GetRectangleBrush()
		{
			return themeService.GetRectangleBrush();
		}


		public void AddRootItem(Item item)
		{
			rootNodes.Add(item);
		}


		public object MoveItem(Point viewPosition, Vector viewOffset, object movingObject)
		{
			Node node = movingObject as Node;

			Point canvasPoint = canvasService.GetCanvasPoint(viewPosition);
			bool isFirst = false;

			if (node == null)
			{
				Point point = new Point(canvasPoint.X - 6 / CanvasScale, canvasPoint.Y - 6 / CanvasScale);
				Rect area = new Rect(point, new Size(6 / CanvasScale, 6 / CanvasScale));

				node = itemsSource
					.GetItemsInArea(area)
					.OfType<Node>()
					.LastOrDefault(item => item.ParentItem != null);
				isFirst = true;
			}
			
			if (node != null)
			{
				MoveNode(node, canvasPoint, viewOffset, isFirst);
			}
			
			return node;
		}



		private void MoveNode(Node node, Point canvasPoint, Vector viewOffset, bool isFirst)
		{
			//module.Move(viewOffset);
			node.MoveOrResize(canvasPoint, viewOffset, isFirst);
		}

		public void RemoveRootNode(Item item)
		{
			rootNodes.Remove(item);
		}


		private void OnScaleChanged()
		{
			rootNodes.ToList().ForEach(node => node.ChangedScale());
		}
	}
}