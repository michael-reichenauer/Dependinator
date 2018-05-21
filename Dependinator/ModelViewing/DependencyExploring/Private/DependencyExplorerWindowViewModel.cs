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
		private readonly IDependenciesService dependenciesService;
		private readonly Window owner;


		private Node sourceNode;
		private Node targetNode;
		private IEnumerable<Line> lines;


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

		public Command<Window> CancelCommand => Command<Window>(w => w.Close());
		public Command SwitchSidesCommand => AsyncCommand(SwitchSidesAsync);

		public ObservableCollection<DependencyItemViewModel> SourceItems { get; } =
			new ObservableCollection<DependencyItemViewModel>();

		public ObservableCollection<DependencyItemViewModel> TargetItems { get; } =
			new ObservableCollection<DependencyItemViewModel>();


		private async void InitializeAsync(Node node, Line line)
		{
			if (line == null)
			{
				sourceNode = node;
				targetNode = node.Root;
				lines = sourceNode.SourceLines.Concat(targetNode.TargetLines);
			}
			else
			{
				sourceNode = line.Source;
				targetNode = line.Target;
				lines = new List<Line> { line };
			}

			await SetSourceAndTargetItemsAsync();
		}


		private async Task SwitchSidesAsync()
		{
			Node node = sourceNode;
			sourceNode = targetNode;
			targetNode = node;
			lines = sourceNode.SourceLines.Concat(targetNode.TargetLines); 

			await SetSourceAndTargetItemsAsync();
		}


		private async Task SetSourceAndTargetItemsAsync()
		{
			await SetDependencyItemsAsync(true);
			await SetDependencyItemsAsync(false);

			SetTexts();
			SelectNode(sourceNode, SourceItems);
			SelectNode(targetNode, TargetItems);
		}


		public void ShowCode(Node node)
		{
			CodeDialog codeDialog = new CodeDialog(owner, node);
			codeDialog.Show();
		}


		public async void FilterOn(DependencyItem dependencyItem, bool isSourceSide)
		{
			bool isAncestor = false;

			if (isSourceSide)
			{
				isAncestor = sourceNode.Ancestors().Contains(dependencyItem.Node);
				sourceNode = dependencyItem.Node;
			}
			else
			{
				isAncestor = targetNode.Ancestors().Contains(dependencyItem.Node);
				targetNode = dependencyItem.Node;
			}

			lines = sourceNode.SourceLines.Concat(targetNode.TargetLines);

			await SetDependencyItemsAsync(!isSourceSide);
			if (isAncestor)
			{
				await SetDependencyItemsAsync(isSourceSide);
			}

			SetTexts();
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


		private async Task SetDependencyItemsAsync(bool isSourceSide)
		{
			var dependencyItems = await dependenciesService.GetReferencesAsync(
				lines, isSourceSide, sourceNode, targetNode);

			var items = isSourceSide ? SourceItems : TargetItems;

			items.Clear();
			dependencyItems
				.Select(item => new DependencyItemViewModel(item, this, isSourceSide))
				.ForEach(item => items.Add(item));
		}


		private void SetTexts()
		{
			SourceText = sourceNode.IsRoot ? "all nodes" : sourceNode.Name.DisplayFullName;
			TargetText = targetNode.IsRoot ? "all nodes" : targetNode.Name.DisplayFullName;
		}


		private void SelectNode(Node node, IEnumerable<DependencyItemViewModel> items)
		{
			foreach (var viewModel in items)
			{
				if (viewModel.Item.Node.IsRoot || node.AncestorsAndSelf().Contains(viewModel.Item.Node))
				{
					viewModel.IsExpanded = true;
					SelectNode(node, viewModel.SubItems);

					if (viewModel.Item.Node == node)
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