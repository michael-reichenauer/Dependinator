using System.Linq;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using Dependinator.Common;
using Dependinator.Common.ThemeHandling;
using Dependinator.ModelViewing.CodeViewing;
using Dependinator.ModelViewing.DependencyExploring;
using Dependinator.ModelViewing.DependencyExploring.Private;
using Dependinator.ModelViewing.Items;
using Dependinator.ModelViewing.ModelHandling.Core;
using Dependinator.ModelViewing.ModelHandling.Private;


namespace Dependinator.ModelViewing.Nodes.Private
{
	internal class NodeViewModelService : INodeViewModelService
	{
		private readonly IModelNodeService modelNodeService;
		private readonly IThemeService themeService;
		private readonly IModelLineService modelLineService;
		private readonly IItemSelectionService itemSelectionService;
		private readonly INodeLayoutService nodeLayoutService;
		private readonly IDependencyExplorerService dependencyExplorerService;
		private readonly IDependencyWindowService dependencyWindowService;
		private readonly WindowOwner owner;



		public NodeViewModelService(
			IModelNodeService modelNodeService,
			IThemeService themeService,
			IModelLineService modelLineService,
			IItemSelectionService itemSelectionService,
			INodeLayoutService nodeLayoutService,
			IDependencyExplorerService dependencyExplorerService,
			IDependencyWindowService dependencyWindowService,
			WindowOwner owner)
		{
			this.modelNodeService = modelNodeService;
			this.themeService = themeService;
			this.modelLineService = modelLineService;
			this.itemSelectionService = itemSelectionService;
			this.nodeLayoutService = nodeLayoutService;
			this.dependencyExplorerService = dependencyExplorerService;
			this.dependencyWindowService = dependencyWindowService;
			this.owner = owner;
		}



		public void HideNode(Node node)
		{
			if (node.View.IsHidden)
			{
				return;
			}

			modelNodeService.HideNode(node);

	
		}


		public Brush GetNodeBrush(Node node)
		{
			return node.View.Color != null
				? Converter.BrushFromHex(node.View.Color)
				: GetRandomRectangleBrush(node.Name.DisplayShortName);
		}


		public void FirstShowNode(Node node)
		{
			node.SourceLines
				.Where(line => line.View.ViewModel == null)
				.ForEach(line => modelLineService.AddLineViewModel(line));

			node.TargetLines
				.Where(line => line.View.ViewModel == null)
				.ForEach(line => modelLineService.AddLineViewModel(line));
		}


		public void MouseClicked(NodeViewModel nodeViewModel) =>
			itemSelectionService.Select(nodeViewModel);


		public void OnMouseWheel(
			NodeViewModel nodeViewModel,
			UIElement uiElement,
			MouseWheelEventArgs e)
		{
			ItemsCanvas itemsCanvas = nodeViewModel.ItemsViewModel?.ItemsCanvas ?? nodeViewModel.Node.Root.View.ItemsCanvas;

			if (nodeViewModel.IsInnerSelected)
			{
				itemsCanvas.ZoomNode(e);
			}
			else
			{
				itemsCanvas.RootCanvas.ZoomNode(e);
			}
		}


		public Brush GetRandomRectangleBrush(string nodeName)
		{
			return themeService.GetRectangleBrush(nodeName);
		}

		public Brush GetBrushFromHex(string hexColor)
		{
			return themeService.GetBrushFromHex(hexColor);
		}

		public string GetHexColorFromBrush(Brush brush)
		{
			return themeService.GetHexColorFromBrush(brush);
		}

		public Brush GetBackgroundBrush(Brush brush)
		{
			return themeService.BackgroundBrush();
		}

		public Brush GetSelectedBrush(Brush brush)
		{
			return themeService.GetRectangleSelectedBackgroundBrush(brush);
		}


		public void ShowReferences(NodeViewModel nodeViewModel) =>
			dependencyExplorerService.ShowWindow(nodeViewModel.Node);


		public void ShowCode(Node node) => dependencyWindowService.ShowCode(node.Name);


		public void RearrangeLayout(NodeViewModel nodeViewModel) =>
			nodeLayoutService.ResetLayout(nodeViewModel.Node);
	}
}
