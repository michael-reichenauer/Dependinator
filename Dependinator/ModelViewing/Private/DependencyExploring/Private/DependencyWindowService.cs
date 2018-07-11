using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Dependinator.Common.MessageDialogs;
using Dependinator.ModelViewing.Private.CodeViewing;
using Dependinator.ModelViewing.Private.ModelHandling;
using Dependinator.ModelViewing.Private.ModelHandling.Core;
using Dependinator.ModelViewing.Private.ModelHandling.Private;
using Dependinator.ModelViewing.Private.Nodes;


namespace Dependinator.ModelViewing.Private.DependencyExploring.Private
{
	internal class DependencyWindowService : IDependencyWindowService
	{
		private readonly IDependenciesService dependenciesService;
		private readonly IModelService modelService;
		private readonly Lazy<IModelNotifications> modelNotifications;
		private readonly IMessage message;
		private readonly ILocateNodeService locateNodeService;
		private readonly Func<NodeName, CodeDialog> codeDialogProvider;
		private readonly Func<Node, Line, DependencyExplorerWindow> dependencyExplorerWindowProvider;


		public DependencyWindowService(
			IDependenciesService dependenciesService,
			IModelService modelService,
			Lazy<IModelNotifications> modelNotifications,
			IMessage message,
			ILocateNodeService locateNodeService,
			Func<NodeName, CodeDialog> codeDialogProvider,
			Func<Node, Line, DependencyExplorerWindow> dependencyExplorerWindowProvider)
		{
			this.dependenciesService = dependenciesService;
			this.modelService = modelService;
			this.modelNotifications = modelNotifications;
			this.message = message;
			this.locateNodeService = locateNodeService;
			this.codeDialogProvider = codeDialogProvider;
			this.dependencyExplorerWindowProvider = dependencyExplorerWindowProvider;
		}


		public void ShowCode(NodeName nodeName)
		{
			CodeDialog codeDialog = codeDialogProvider(nodeName);
			codeDialog.Show();
		}


		public void Initialize(DependencyExplorerWindowViewModel viewModel, Node node, Line line)
		{
			if (node != null)
			{
				InitializeFromNodeAsync(viewModel, node);
			}
			else
			{
				InitializeFromLineAsync(viewModel, line);
			}
		}


		public async Task SwitchSidesAsync(DependencyExplorerWindowViewModel viewModel)
		{
			NodeName nodeName = viewModel.SourceNodeName;
			viewModel.SourceNodeName = viewModel.TargetNodeName;
			viewModel.TargetNodeName = nodeName;

			await SetSidesAsync(viewModel);
		}


		public async Task RefreshAsync(DependencyExplorerWindowViewModel viewModel)
		{
			await RefreshModelAsync();
		}


		public async void FilterOn(
			DependencyExplorerWindowViewModel viewModel,
			DependencyItem dependencyItem,
			bool isSourceSide)
		{
			if (!TryGetNode(viewModel.SourceNodeName, out Node sourceNode))
			{
				message.ShowInfo(
					$"The source node no longer exists in the model:\n{viewModel.SourceNodeName.DisplayLongName}");
				return;
			}

			if (!TryGetNode(viewModel.TargetNodeName, out Node targetNode))
			{
				message.ShowInfo(
					$"The target node no longer exists in the model:\n{viewModel.TargetNodeName.DisplayLongName}");
				return;
			}

			if (!TryGetNode(dependencyItem.NodeName, out Node itemNode))
			{
				message.ShowInfo(
					$"The clicked node no longer exists in the model:\n{dependencyItem.NodeName.DisplayLongName}");

				return;
			}

			await FilterOn(viewModel, sourceNode, targetNode, itemNode, isSourceSide);
		}


		public async Task Refresh(DependencyExplorerWindowViewModel viewModel)
		{
			Node sourceNode = GetNodeOrParent(viewModel.SourceNodeName);
			Node targetNode = GetNodeOrParent(viewModel.TargetNodeName);

			await SetSourceAndTargetItemsAsync(viewModel, sourceNode, targetNode);
		}


		public void Locate(NodeName nodeName) => locateNodeService.StartMoveToNode(nodeName);
		public void ShowDependencies(NodeName nodeName)
		{
			if (TryGetNode(nodeName, out Node node))
			{
				var explorerWindow = dependencyExplorerWindowProvider(node, null);
				explorerWindow.Show();
			}
		}


		private async void InitializeFromNodeAsync(DependencyExplorerWindowViewModel viewModel, Node node)
		{
			Node sourceNode;
			Node targetNode;

			// By default assume node is source if there are source lines (or node is no target) 
			if (node.SourceLines.Any(l => l.Owner != node) || !node.TargetLines.Any())
			{
				sourceNode = node;
				targetNode = node.Root;
			}
			else
			{
				// Node has no source lines so node as target
				sourceNode = node.Root;
				targetNode = node;
			}

			await SetSourceAndTargetItemsAsync(viewModel, sourceNode, targetNode);
		}


		private async void InitializeFromLineAsync(DependencyExplorerWindowViewModel viewModel, Line line)
		{
			// For lines to/from parent, use root 
			Node sourceNode = line.Source;
			Node targetNode = line.Target;

			await SetSourceAndTargetItemsAsync(viewModel, sourceNode, targetNode);
		}


		private async Task SetSidesAsync(DependencyExplorerWindowViewModel viewModel)
		{
			if (!TryGetNode(viewModel.SourceNodeName, out Node sourceNode))
			{
				message.ShowInfo(
					$"The source node no longer exists in the model:\n{viewModel.SourceNodeName.DisplayLongName}");
				return;
			}

			if (!TryGetNode(viewModel.TargetNodeName, out Node targetNode))
			{
				message.ShowInfo(
					$"The target node no longer exists in the model:\n{viewModel.TargetNodeName.DisplayLongName}");

				return;
			}

			await SetSourceAndTargetItemsAsync(viewModel, sourceNode, targetNode);
		}


		private async Task SetSourceAndTargetItemsAsync(
			DependencyExplorerWindowViewModel viewModel,
			Node sourceNode,
			Node targetNode)
		{
			viewModel.SourceNodeName = sourceNode.Name;
			viewModel.TargetNodeName = targetNode.Name;

			await SetDependencyItemsAsync(viewModel, sourceNode, targetNode, true);
			await SetDependencyItemsAsync(viewModel, sourceNode, targetNode, false);

			await SetTextsAsync(viewModel, sourceNode, targetNode);
			SelectNode(sourceNode, viewModel.SourceItems);
			SelectNode(targetNode, viewModel.TargetItems);
		}


		private async Task FilterOn(
			DependencyExplorerWindowViewModel viewModel,
			Node sourceNode,
			Node targetNode,
			Node itemNode,
			bool isSourceSide)
		{
			bool isAncestor = false;

			if (isSourceSide)
			{
				isAncestor = sourceNode.Ancestors().Contains(itemNode);
				sourceNode = itemNode;
			}
			else
			{
				isAncestor = targetNode.Ancestors().Contains(itemNode);
				targetNode = itemNode;
			}

			await SetDependencyItemsAsync(viewModel, sourceNode, targetNode, !isSourceSide);
			if (isAncestor)
			{
				await SetDependencyItemsAsync(viewModel, sourceNode, targetNode, isSourceSide);
			}

			await SetTextsAsync(viewModel, sourceNode, targetNode);
			if (isSourceSide)
			{
				SelectNode(targetNode, viewModel.TargetItems);
				if (isAncestor)
				{
					SelectNode(sourceNode, viewModel.SourceItems);
				}
			}
			else
			{
				SelectNode(sourceNode, viewModel.SourceItems);
				if (isAncestor)
				{
					SelectNode(targetNode, viewModel.TargetItems);
				}
			}
		}


		private async Task SetDependencyItemsAsync(
			DependencyExplorerWindowViewModel viewModel,
			Node sourceNode,
			Node targetNode,
			bool isSourceSide)
		{
			viewModel.SourceNodeName = sourceNode.Name;
			viewModel.TargetNodeName = targetNode.Name;

			var dependencyItems = await GetDependencyItemsAsync(isSourceSide, sourceNode, targetNode);

			var items = isSourceSide ? viewModel.SourceItems : viewModel.TargetItems;

			items.Clear();

			dependencyItems
				.Select(item => new DependencyItemViewModel(item, viewModel, isSourceSide))
				.ForEach(item => items.Add(item));
		}


		private Task<IReadOnlyList<DependencyItem>> GetDependencyItemsAsync(
			bool isSourceSide, Node sourceNode, Node targetNode) =>
			dependenciesService.GetDependencyItemsAsync(isSourceSide, sourceNode, targetNode);


		private async Task SetTextsAsync(
			DependencyExplorerWindowViewModel viewModel,
			Node sourceNode,
			Node targetNode)
		{
			await Task.Yield();
			NodeName sourceName = sourceNode == targetNode.Parent ? NodeName.Root : viewModel.SourceNodeName;
			NodeName targetName = targetNode == sourceNode.Parent ? NodeName.Root : viewModel.TargetNodeName;
			viewModel.SourceText = ToNodeText(sourceName);
			viewModel.TargetText = ToNodeText(targetName);
		}


		private static string ToNodeText(NodeName nodeName) =>
			nodeName == NodeName.Root ? "all nodes" : nodeName.DisplayLongName;


		private static void SelectNode(Node node, IEnumerable<DependencyItemViewModel> items)
		{
			foreach (var viewModel in items)
			{
				if (viewModel.Item.NodeName == NodeName.Root ||
						node.AncestorsAndSelf().Any(n => n.Name == viewModel.Item.NodeName))
				{
					viewModel.IsExpanded = true;
					SelectNode(node, viewModel.SubItems);

					if (viewModel.Item.NodeName == node.Name)
					{
						viewModel.IsSelected = true;
						ExpandFirst(viewModel.SubItems);
					}

					break;
				}
			}
		}


		private static void ExpandFirst(IEnumerable<DependencyItemViewModel> items)
		{
			while (items.Count() == 1)
			{
				var viewModel = items.First();
				viewModel.IsExpanded = true;
				items = viewModel.SubItems;
			}
		}



		private bool TryGetNode(NodeName nodeName, out Node node) =>
			modelService.TryGetNode(nodeName, out node);


		private Node GetNodeOrParent(NodeName nodeName)
		{
			while (true)
			{
				if (modelService.TryGetNode(nodeName, out Node node))
				{
					return node;
				}

				nodeName = nodeName.ParentName;
			}
		}


		private Task RefreshModelAsync() => modelNotifications.Value.ManualRefreshAsync(false);
	}
}