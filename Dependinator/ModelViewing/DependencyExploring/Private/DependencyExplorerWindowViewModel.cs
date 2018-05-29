using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using Dependinator.Common;
using Dependinator.ModelViewing.CodeViewing;
using Dependinator.ModelViewing.ModelHandling.Core;
using Dependinator.Utils.UI.Mvvm;


namespace Dependinator.ModelViewing.DependencyExploring.Private
{
	internal class DependencyExplorerWindowViewModel : ViewModel, IItemCommands
	{
		private readonly IDependenciesService dependenciesService;
		private readonly Window owner;


		private NodeName sourceNodeName;
		private NodeName targetNodeName;
		//private IEnumerable<Line> lines;


		public DependencyExplorerWindowViewModel(
			IDependenciesService dependenciesService, 
			Window owner,
			Node node, 
			Line line)
		{
			this.dependenciesService = dependenciesService;
			this.owner = owner;

			InitializeAsync(node, line);
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


		private async void InitializeAsync(Node node, Line line)
		{
			Node sourceNode;
			Node targetNode;
			IReadOnlyList<Line> lines;

			if (line == null)
			{
				if (node.SourceLines.Any(l => l.Owner != node))
				{
					sourceNode = node;
					targetNode = node.Root;
					lines = sourceNode.SourceLines;
				}
				else
				{
					sourceNode = node.Root;
					targetNode = node;
					lines = targetNode.TargetLines;
				}
			}
			else
			{
				// For lines to/from parent, use root 
				sourceNode = line.Source == line.Target.Parent ? line.Source.Root : line.Source;
				targetNode = line.Target == line.Source.Parent ? line.Target.Root : line.Target;
				lines = new List<Line> { line };
			}

			await SetSourceAndTargetItemsAsync(sourceNode, targetNode, lines);
		}


		private async Task SwitchSidesAsync()
		{
			NodeName nodeName = sourceNodeName;
			sourceNodeName = targetNodeName;
			targetNodeName = nodeName;

			if (dependenciesService.TryGetNode(sourceNodeName, out Node sourceNode) &&
			    dependenciesService.TryGetNode(targetNodeName, out Node targetNode))
			{
				IReadOnlyList<Line> lines = sourceNode.SourceLines.Concat(targetNode.TargetLines).ToList();

				await SetSourceAndTargetItemsAsync(sourceNode, targetNode, lines);
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


		public void ShowCode(string title, Lazy<string> codeText)
		{
			CodeDialog codeDialog = new CodeDialog(owner, title, codeText);
			codeDialog.Show();
		}


		public async void FilterOn(DependencyItem dependencyItem, bool isSourceSide)
		{
			bool isAncestor = false;
			
			if (!(dependenciesService.TryGetNode(sourceNodeName, out Node sourceNode) &&
			    dependenciesService.TryGetNode(targetNodeName, out Node targetNode) &&
					dependenciesService.TryGetNode(dependencyItem.NodeName, out Node itemNode)))
			{
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

			await SetDependencyItemsAsync(sourceNode, targetNode, lines,!isSourceSide);
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

			var dependencyItems = await dependenciesService.GetDependencyItemsAsync(
				lines, isSourceSide, sourceNode, targetNode);

			var items = isSourceSide ? SourceItems : TargetItems;

			items.Clear();
			dependencyItems
				.Select(item => new DependencyItemViewModel(item, this, isSourceSide))
				.ForEach(item => items.Add(item));
		}


		private async Task RefreshAsync()
		{
			SourceItems.Clear();
			TargetItems.Clear();

			await dependenciesService.RefreshModelAsync();

			if (dependenciesService.TryGetNode(sourceNodeName, out Node sourceNode) &&
			    dependenciesService.TryGetNode(targetNodeName, out Node targetNode))
			{
				IReadOnlyList<Line> lines = sourceNode.SourceLines.Concat(targetNode.TargetLines).ToList();

				await SetSourceAndTargetItemsAsync(sourceNode, targetNode, lines);
			}
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