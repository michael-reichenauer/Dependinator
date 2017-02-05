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
	internal class NodeService : INodeService
	{
		private readonly ICanvasService canvasService;
		private readonly IMainViewItemsSource itemsSource;
		private readonly IThemeService themeService;

		private readonly List<Item> rootNodes = new List<Item>();

		public NodeService(
			ICanvasService canvasService,
			IMainViewItemsSource itemsSource,
			IThemeService themeService)
		{
			this.canvasService = canvasService;
			this.itemsSource = itemsSource;
			this.themeService = themeService;

			canvasService.ScaleChanged += (s, e) => OnScaleChanged();
		}


		public double Scale
		{
			get { return canvasService.Scale; }
			set { canvasService.Scale = value; }			
		} 

		public Rect CurrentViewPort => canvasService.CurrentViewPort;

		public Point Offset => canvasService.Offset;


		public void ShowRootNode(Item item)
		{
			item.ItemRealized();
		}

		public void ClearAll()
		{
			itemsSource.Clear();
		}

		public void ShowNodes(IEnumerable<Item> nodes)
		{
			itemsSource.Add(nodes);
		}


		public void HideNodes(IEnumerable<Item> nodes)
		{
			itemsSource.Remove(nodes);
		}


		public void ShowNode(Item item)
		{
			itemsSource.Add(item);
		}


		public void HideNode(Item item)
		{
			itemsSource.Remove(item);
		}

		public void UpdateNode(Item item)
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


		public void AddRootNode(Item item)
		{
			rootNodes.Add(item);
		}


		public object MoveNode(Point viewPosition, Vector viewOffset, object movingObject)
		{
			Module module = movingObject as Module;

			Point canvasPoint = canvasService.GetCanvasPoint(viewPosition);
			bool isFirst = false;

			if (module == null)
			{
				Point point = new Point(canvasPoint.X - 6 / Scale, canvasPoint.Y - 6 / Scale);
				Rect area = new Rect(point, new Size(6 / Scale, 6 / Scale));

				module = itemsSource
					.GetItemsInArea(area)
					.OfType<Module>()
					.LastOrDefault(node => node.ParentItem != null);
				isFirst = true;
			}
			
			if (module != null)
			{
				MoveNode(module, canvasPoint, viewOffset, isFirst);
			}
			
			return module;
		}



		private void MoveNode(Module module, Point canvasPoint, Vector viewOffset, bool isFirst)
		{
			//module.Move(viewOffset);
			module.MoveOrResize(canvasPoint, viewOffset, isFirst);
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