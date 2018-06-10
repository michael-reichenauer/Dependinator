using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Dependinator.Common;
using Dependinator.Common.MessageDialogs;
using Dependinator.ModelViewing.CodeViewing;
using Dependinator.ModelViewing.ModelHandling.Core;
using Dependinator.ModelViewing.ModelHandling.Private;


namespace Dependinator.ModelViewing.DependencyExploring.Private
{
	internal class DependencyWindowService : IDependencyWindowService
	{
		private readonly IDependenciesService dependenciesService;
		private readonly IModelService modelService;
		private readonly Lazy<IModelNotifications> modelNotifications;
		private readonly IMessage message;
		private readonly WindowOwner owner;


		public DependencyWindowService(
			IDependenciesService dependenciesService,
			IModelService modelService,
			Lazy<IModelNotifications> modelNotifications,
			IMessage message,
			WindowOwner owner)
		{
			this.dependenciesService = dependenciesService;
			this.modelService = modelService;
			this.modelNotifications = modelNotifications;
			this.message = message;
			this.owner = owner;
		}


		public void ShowCode(NodeName nodeName)
		{
			if (!TryGetNode(nodeName, out Node node))
			{
				message.ShowInfo($"The node no longer exists in the model:\n{nodeName.DisplayFullName}");
				return;
			}
			
			CodeDialog codeDialog = new CodeDialog(owner, nodeName.DisplayFullName, node.CodeText);
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

			await SetSidesAsync(viewModel);
		}


		public async void FilterOn(
			DependencyExplorerWindowViewModel viewModel,
			DependencyItem dependencyItem,
			bool isSourceSide)
		{
			if (!TryGetNode(viewModel.SourceNodeName, out Node sourceNode))
			{
				message.ShowInfo(
					$"The source node no longer exists in the model:\n{viewModel.SourceNodeName.DisplayFullName}");
				return;
			}

			if (!TryGetNode(viewModel.TargetNodeName, out Node targetNode))
			{
				message.ShowInfo(
					$"The target node no longer exists in the model:\n{viewModel.TargetNodeName.DisplayFullName}");
				return;
			}

			if (!TryGetNode(dependencyItem.NodeName, out Node itemNode))
			{
				message.ShowInfo(
					$"The clicked node no longer exists in the model:\n{dependencyItem.NodeName.DisplayFullName}");

				return;
			}

			await FilterOn(viewModel, sourceNode, targetNode, itemNode, isSourceSide);
		}



		private async void InitializeFromNodeAsync(DependencyExplorerWindowViewModel viewModel, Node node)
		{
			Node sourceNode;
			Node targetNode;
			IReadOnlyList<Line> lines;

			// By default assume node is source if there are source lines (or node is no target) 
			if (node.SourceLines.Any(l => l.Owner != node) || !node.TargetLines.Any())
			{
				sourceNode = node;
				targetNode = node.Root;
				lines = sourceNode.SourceLines;
			}
			else
			{
				// Node has no source lines so node as target
				sourceNode = node.Root;
				targetNode = node;
				lines = targetNode.TargetLines;
			}

			await SetSourceAndTargetItemsAsync(viewModel, sourceNode, targetNode, lines);
		}



		private async void InitializeFromLineAsync(DependencyExplorerWindowViewModel viewModel, Line line)
		{
			// For lines to/from parent, use root 
			Node sourceNode = line.Source == line.Target.Parent ? line.Source.Root : line.Source;
			Node targetNode = line.Target == line.Source.Parent ? line.Target.Root : line.Target;
			IReadOnlyList<Line> lines = new List<Line> { line };

			await SetSourceAndTargetItemsAsync(viewModel, sourceNode, targetNode, lines);
		}




		private async Task SetSidesAsync(DependencyExplorerWindowViewModel viewModel)
		{
			if (!TryGetNode(viewModel.SourceNodeName, out Node sourceNode))
			{
				message.ShowInfo(
					$"The source node no longer exists in the model:\n{viewModel.SourceNodeName.DisplayFullName}");
				return;
			}

			if (!TryGetNode(viewModel.TargetNodeName, out Node targetNode))
			{
				message.ShowInfo(
					$"The target node no longer exists in the model:\n{viewModel.TargetNodeName.DisplayFullName}");

				return;
			}


			IReadOnlyList<Line> lines = sourceNode.SourceLines.Concat(targetNode.TargetLines).ToList();

			await SetSourceAndTargetItemsAsync(viewModel, sourceNode, targetNode, lines);
		}



		private async Task SetSourceAndTargetItemsAsync(
			DependencyExplorerWindowViewModel viewModel,
			Node sourceNode,
			Node targetNode,
			IReadOnlyList<Line> lines)
		{
			viewModel.SourceNodeName = sourceNode.Name;
			viewModel.TargetNodeName = targetNode.Name;

			await SetDependencyItemsAsync(viewModel, sourceNode, targetNode, lines, true);
			await SetDependencyItemsAsync(viewModel, sourceNode, targetNode, lines, false);

			await SetTextsAsync(viewModel);
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

			IReadOnlyList<Line> lines = sourceNode.SourceLines.Concat(targetNode.TargetLines).ToList();

			await SetDependencyItemsAsync(viewModel, sourceNode, targetNode, lines, !isSourceSide);
			if (isAncestor)
			{
				await SetDependencyItemsAsync(viewModel, sourceNode, targetNode, lines, isSourceSide);
			}

			await SetTextsAsync(viewModel);
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
			IReadOnlyList<Line> lines,
			bool isSourceSide)
		{
			viewModel.SourceNodeName = sourceNode.Name;
			viewModel.TargetNodeName = targetNode.Name;

			var dependencyItems = await GetDependencyItemsAsync(
				lines, isSourceSide, sourceNode, targetNode);

			var items = isSourceSide ? viewModel.SourceItems : viewModel.TargetItems;

			items.Clear();

			dependencyItems
				.Select(item => new DependencyItemViewModel(item, viewModel, isSourceSide))
				.ForEach(item => items.Add(item));
		}


		private Task<IReadOnlyList<DependencyItem>> GetDependencyItemsAsync(
			IReadOnlyList<Line> lines, bool isSourceSide, Node sourceNode, Node targetNode) =>
			dependenciesService.GetDependencyItemsAsync(lines, isSourceSide, sourceNode, targetNode);


		private async Task SetTextsAsync(DependencyExplorerWindowViewModel viewModel)
		{
			await Task.Yield();
			viewModel.SourceText = ToNodeText(viewModel.SourceNodeName);
			viewModel.TargetText = ToNodeText(viewModel.TargetNodeName);
		}


		private static string ToNodeText(NodeName nodeName) =>
			nodeName == NodeName.Root ? "all nodes" : nodeName.DisplayFullName;


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


		private Task RefreshModelAsync() => modelNotifications.Value.ManualRefreshAsync(false);
	}
}