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

		private readonly List<Node> rootNodes = new List<Node>();

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


		public double Scale => canvasService.Scale;

		public Rect CurrentViewPort => canvasService.CurrentViewPort;

		public Point Offset => canvasService.Offset;


		public void ShowNodes(IEnumerable<Node> nodes)
		{
			itemsSource.Add(nodes);
		}


		public void HideNodes(IEnumerable<Node> nodes)
		{
			itemsSource.Remove(nodes);
		}


		public void ShowNode(Node node)
		{
			itemsSource.Add(node);
		}


		public void HideNode(Node node)
		{
			itemsSource.Remove(node);
		}

		public void UpdateNode(Node node)
		{
			itemsSource.Update(node);
		}


		public Brush GetRectangleBackgroundBrush(Brush brush)
		{
			return themeService.GetRectangleBackgroundBrush(brush);
		}


		public Brush GetRectangleBrush()
		{
			return themeService.GetRectangleBrush();
		}


		public void AddRootNode(Node node)
		{
			rootNodes.Add(node);
		}


		public object MoveNode(Point viewPosition, Vector viewOffset, object movingObject)
		{
			Module module = movingObject as Module;

			if (module == null)
			{
				Point point = canvasService.GetCanvasPoint(viewPosition);

				Rect area = new Rect(point, new Size(1 / Scale, 1 / Scale));

				module = itemsSource
					.GetItemsInArea(area)
					.OfType<Module>()
					.LastOrDefault(node => node.ParentNode != null);
			}
			
			if (module != null)
			{
				MoveNode(module, viewOffset);
			}
			
			return module;
		}



		private void MoveNode(Module module, Vector viewOffset)
		{
			module.Move(viewOffset);
		}

		public void RemoveRootNode(Node node)
		{
			rootNodes.Remove(node);
		}


		private void OnScaleChanged()
		{
			rootNodes.ToList().ForEach(node => node.ChangedScale());
		}
	}
}