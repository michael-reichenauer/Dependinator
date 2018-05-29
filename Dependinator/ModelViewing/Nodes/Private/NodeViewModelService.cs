using System.Collections.Generic;
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
using Dependinator.ModelViewing.Lines.Private;
using Dependinator.ModelViewing.ModelHandling.Core;
using Dependinator.ModelViewing.ModelHandling.Private;


namespace Dependinator.ModelViewing.Nodes.Private
{
	internal class NodeViewModelService : INodeViewModelService
	{
		private readonly IThemeService themeService;
		private readonly IModelLineService modelLineService;
		private readonly ILineMenuItemService lineMenuItemService;
		private readonly IItemSelectionService itemSelectionService;
		private readonly IDependenciesService dependenciesService;
		private readonly WindowOwner owner;



		public NodeViewModelService(
			IThemeService themeService,
			IModelLineService modelLineService,
			ILineMenuItemService lineMenuItemService,
			IItemSelectionService itemSelectionService,
			IDependenciesService dependenciesService,
			WindowOwner owner)
		{
			this.themeService = themeService;
			this.modelLineService = modelLineService;
			this.lineMenuItemService = lineMenuItemService;
			this.itemSelectionService = itemSelectionService;
			this.dependenciesService = dependenciesService;
			this.owner = owner;
		}



		public Brush GetNodeBrush(Node node)
		{
			return node.View.Color != null
				? Converter.BrushFromHex(node.View.Color)
				: GetRandomRectangleBrush(node.Name.DisplayName);
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



		public IEnumerable<LineMenuItemViewModel> GetIncomingLinkItems(Node node)
		{
			IEnumerable<Line> lines = node.TargetLines
				.Where(line => line.Owner != node);

			return lineMenuItemService.GetSourceLinkItems(lines);
		}


		public IEnumerable<LineMenuItemViewModel> GetOutgoingLinkItems(Node node)
		{
			IEnumerable<Line> lines = node.SourceLines
				.Where(line => line.Owner != node);

			return lineMenuItemService.GetTargetLinkItems(lines);
		}


		public void MouseClicked(NodeViewModel nodeViewModel) => 
			itemSelectionService.Select(nodeViewModel);


		public void OnMouseWheel(
			NodeViewModel nodeViewModel,
			UIElement uiElement,
			MouseWheelEventArgs e)
		{
			ItemsCanvas itemsCanvas = nodeViewModel.ItemsViewModel?.ItemsCanvas ?? nodeViewModel.Node.Root.View.ItemsCanvas;

			itemsCanvas.OnMouseWheel(uiElement, e, nodeViewModel.IsInnerSelected);
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


		public void ShowReferences(NodeViewModel nodeViewModel)
		{
			Node node = nodeViewModel.Node;

			DependencyExplorerWindow dependencyExplorerWindow = new DependencyExplorerWindow(dependenciesService, owner, node);
			dependencyExplorerWindow.Show();
		}


		public void ShowCode(Node node)
		{
			CodeDialog codeDialog = new CodeDialog(owner, node.Name.DisplayFullName, node.CodeText);
			codeDialog.Show();
		}


		public Brush GetRectangleHighlightBrush(Brush brush)
		{
			return themeService.GetRectangleHighlighterBrush(brush);
		}
	}
}