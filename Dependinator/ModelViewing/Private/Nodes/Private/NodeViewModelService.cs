using System;
using System.Linq;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using Dependinator.Common.ModelMetadataFolders;
using Dependinator.Common.ThemeHandling;
using Dependinator.ModelViewing.Private.CodeViewing;
using Dependinator.ModelViewing.Private.DependencyExploring;
using Dependinator.ModelViewing.Private.ItemsViewing;
using Dependinator.ModelViewing.Private.ModelHandling.Core;
using Dependinator.ModelViewing.Private.ModelHandling.Private;
using DependinatorApi;
using DependinatorApi.ApiHandling;


namespace Dependinator.ModelViewing.Private.Nodes.Private
{
	internal class NodeViewModelService : INodeViewModelService
	{
		private readonly IModelService modelService;
		private readonly IThemeService themeService;
		private readonly ISelectionService selectionService;
		private readonly INodeLayoutService nodeLayoutService;
		private readonly Func<Node, Line, DependencyExplorerWindow> dependencyExplorerWindowProvider;
		private readonly Func<NodeName, CodeDialog> codeDialogProvider;
		private readonly ModelMetadata modelMetadata;


		public NodeViewModelService(
			IModelService modelService,
			IThemeService themeService,
			ISelectionService selectionService,
			INodeLayoutService nodeLayoutService,
			Func<Node, Line, DependencyExplorerWindow> dependencyExplorerWindowProvider,
			Func<NodeName, CodeDialog> codeDialogProvider,
			ModelMetadata modelMetadata)
		{
			this.modelService = modelService;
			this.themeService = themeService;
			this.selectionService = selectionService;
			this.nodeLayoutService = nodeLayoutService;
			this.dependencyExplorerWindowProvider = dependencyExplorerWindowProvider;
			this.codeDialogProvider = codeDialogProvider;
			this.modelMetadata = modelMetadata;
		}



		public void HideNode(Node node)
		{
			if (node.IsHidden)
			{
				return;
			}

			modelService.HideNode(node);
		}

		public void ShowNode(Node node)
		{
			if (!node.IsHidden)
			{
				return;
			}

			modelService.ShowHiddenNode(node.Name);
		}


		public void SetIsChanged() => modelService.SetIsChanged();



		public Brush GetDimBrush() => themeService.GetDimBrush();
		public Brush GetTitleBrush() => themeService.GetTextBrush();


		public Brush GetNodeBrush(Node node)
		{
			return node.Color != null
				? Converter.BrushFromHex(node.Color)
				: GetRandomRectangleBrush(node.Name.DisplayShortName);
		}


		public void FirstShowNode(Node node)
		{
			node.SourceLines
				.Where(line => line.View.ViewModel == null)
				.ForEach(line => modelService.AddLineViewModel(line));

			node.TargetLines
				.Where(line => line.View.ViewModel == null)
				.ForEach(line => modelService.AddLineViewModel(line));
		}


		public void MouseClicked(NodeViewModel nodeViewModel) =>
			selectionService.Select(nodeViewModel);


		public void OnMouseWheel(
			NodeViewModel nodeViewModel,
			UIElement uiElement,
			MouseWheelEventArgs e)
		{
			ItemsCanvas itemsCanvas = nodeViewModel.ItemsViewModel?.ItemsCanvas ?? nodeViewModel.Node.Root.ItemsCanvas;

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


		public void ShowReferences(NodeViewModel nodeViewModel)
		{
			DependencyExplorerWindow window = dependencyExplorerWindowProvider(nodeViewModel.Node, null);
			window.Show();
		}


		public void ShowCode(Node node)
		{
			string serverName = ApiServerNames.ExtensionApiServerName(modelMetadata.ModelFilePath);
			if (ApiIpcClient.IsServerRegistered(serverName))
			{
				using (ApiIpcClient apiIpcClient = new ApiIpcClient(serverName))
				{
					apiIpcClient.Service<IVsExtensionApi>().ShowFile(
						@"C:\Work Files\GitMind\GitMind\RepositoryViews\CommitViewModel.cs");
				}

				return;
			}

			CodeDialog codeDialog = codeDialogProvider(node.Name);
			codeDialog.Show();
		}


		public void RearrangeLayout(NodeViewModel nodeViewModel)
		{
			nodeLayoutService.ResetLayout(nodeViewModel.Node);
			modelService.SetIsChanged();
		}
	}
}
