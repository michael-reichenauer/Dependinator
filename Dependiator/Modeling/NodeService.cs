using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Media;
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

		private List<Node> rootNodes = new List<Node>();

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

		public void ShowNodes(IEnumerable<Node> nodes)
		{
			itemsSource.Add(nodes);
		}

		public void ShowNode(Node node)
		{
			itemsSource.Add(node);
		}


		public void HideNode(Node node)
		{
			itemsSource.Remove(node);
		}


		public Brush GetNextBrush()
		{
			return themeService.GetNextBrush();
		}


		public void AddRootNode(Node node)
		{
			rootNodes.Add(node);
		}


		public void RemoveRootNode(Node node)
		{
			rootNodes.Remove(node);
		}


		private void OnScaleChanged()
		{
			IEnumerable<IItem> itemsInView = itemsSource.GetItemsInView().ToList();

			itemsInView
				.ForEach(item => item.ChangedScale());

			rootNodes
				.Where(node => !node.IsAdded)
				.ToList()
				.ForEach(node => node.TryAddNode());
		}
	}
}