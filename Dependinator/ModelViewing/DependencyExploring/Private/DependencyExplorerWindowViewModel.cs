using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using Dependinator.ModelViewing.CodeViewing;
using Dependinator.ModelViewing.ModelHandling.Core;
using Dependinator.Utils.UI.Mvvm;


namespace Dependinator.ModelViewing.DependencyExploring.Private
{
	internal class DependencyExplorerWindowViewModel : ViewModel, IItemCommands
	{
		private readonly IDependencyWindowService dependencyWindowService;
		private readonly Window owner;

		private NodeName sourceNodeName;
		private NodeName targetNodeName;


		public DependencyExplorerWindowViewModel(
			IDependencyWindowService dependencyWindowService,
			Window owner,
			Node node,
			Line line)
		{
			this.dependencyWindowService = dependencyWindowService;
			this.owner = owner;

			if (node != null)
			{
				InitializeFromNodeAsync(node);
			}
			else
			{
				InitializeFromLineAsync(line);
			}
		}


		public string SourceText { get => Get(); set => Set(value); }

		public string TargetText { get => Get(); set => Set(value); }
		public string SourceTargetToolTip { get => Get(); set => Set(value); }


		public Command<Window> CancelCommand => Command<Window>(w => w.Close());
		public Command SwitchSidesCommand => AsyncCommand(SwitchSidesAsync);

		public Command RefreshCommand => AsyncCommand(RefreshAsync);


		public ObservableCollection<DependencyItemViewModel> SourceItems { get; } =
			new ObservableCollection<DependencyItemViewModel>();

		public ObservableCollection<DependencyItemViewModel> TargetItems { get; } =
			new ObservableCollection<DependencyItemViewModel>();


		public void ShowCode(NodeName nodeName) => dependencyWindowService.ShowCode(nodeName);

		

		private async void InitializeFromNodeAsync(Node node)
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

			await SetSourceAndTargetItemsAsync(sourceNode, targetNode, lines);
		}



		private async void InitializeFromLineAsync(Line line)
		{
			// For lines to/from parent, use root 
			Node sourceNode = line.Source == line.Target.Parent ? line.Source.Root : line.Source;
			Node targetNode = line.Target == line.Source.Parent ? line.Target.Root : line.Target;
			IReadOnlyList<Line> lines = new List<Line> { line };

			await SetSourceAndTargetItemsAsync(sourceNode, targetNode, lines);
		}



		private async Task SwitchSidesAsync()
		{
			NodeName nodeName = sourceNodeName;
			sourceNodeName = targetNodeName;
			targetNodeName = nodeName;

			if (dependencyWindowService.TryGetNode(sourceNodeName, out Node sourceNode) &&
			    dependencyWindowService.TryGetNode(targetNodeName, out Node targetNode))
			{
				IReadOnlyList<Line> lines = sourceNode.SourceLines.Concat(targetNode.TargetLines).ToList();

				await SetSourceAndTargetItemsAsync(sourceNode, targetNode, lines);
			}
			else
			{
				// source and/or target no longer exists ?????
			}
		}



		private async Task RefreshAsync()
		{
			SourceItems.Clear();
			TargetItems.Clear();

			await dependencyWindowService.RefreshModelAsync();

			if (dependencyWindowService.TryGetNode(sourceNodeName, out Node sourceNode) &&
			    dependencyWindowService.TryGetNode(targetNodeName, out Node targetNode))
			{
				IReadOnlyList<Line> lines = sourceNode.SourceLines.Concat(targetNode.TargetLines).ToList();

				await SetSourceAndTargetItemsAsync(sourceNode, targetNode, lines);
			}
			else
			{
				// source and/or target no longer exists ?????
			}
		}





		private async Task SetSourceAndTargetItemsAsync(
			Node sourceNode, Node targetNode, IReadOnlyList<Line> lines)
		{
			sourceNodeName = sourceNode.Name;
			targetNodeName = targetNode.Name;

			await SetDependencyItemsAsync(sourceNode, targetNode, lines, true);
			await SetDependencyItemsAsync(sourceNode, targetNode, lines, false);

			await SetTextsAsync();
			SelectNode(sourceNode, SourceItems);
			SelectNode(targetNode, TargetItems);
		}



		public async void FilterOn(DependencyItem dependencyItem, bool isSourceSide)
		{
			bool isAncestor = false;

			if (!(dependencyWindowService.TryGetNode(sourceNodeName, out Node sourceNode) &&
			      dependencyWindowService.TryGetNode(targetNodeName, out Node targetNode) &&
			      dependencyWindowService.TryGetNode(dependencyItem.NodeName, out Node itemNode)))
			{
				// source and/or target no longer exists ?????
				return;
			}


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

			await SetDependencyItemsAsync(sourceNode, targetNode, lines, !isSourceSide);
			if (isAncestor)
			{
				await SetDependencyItemsAsync(sourceNode, targetNode, lines, isSourceSide);
			}

			await SetTextsAsync();
			if (isSourceSide)
			{
				SelectNode(targetNode, TargetItems);
				if (isAncestor)
				{
					SelectNode(sourceNode, SourceItems);
				}
			}
			else
			{
				SelectNode(sourceNode, SourceItems);
				if (isAncestor)
				{
					SelectNode(targetNode, TargetItems);
				}
			}
		}


		private async Task SetDependencyItemsAsync(
			Node sourceNode, Node targetNode, IReadOnlyList<Line> lines, bool isSourceSide)
		{
			sourceNodeName = sourceNode.Name;
			targetNodeName = targetNode.Name;

			var dependencyItems = await dependencyWindowService.GetDependencyItemsAsync(
				lines, isSourceSide, sourceNode, targetNode);

			var items = isSourceSide ? SourceItems : TargetItems;

			items.Clear();
			dependencyItems
				.Select(item => new DependencyItemViewModel(item, this, isSourceSide))
				.ForEach(item => items.Add(item));
		}


		private async Task SetTextsAsync()
		{
			await Task.Yield();
			SourceText = sourceNodeName == NodeName.Root ? "all nodes" : sourceNodeName.DisplayFullName;
			TargetText = targetNodeName == NodeName.Root ? "all nodes" : targetNodeName.DisplayFullName;
		}


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
	}
}