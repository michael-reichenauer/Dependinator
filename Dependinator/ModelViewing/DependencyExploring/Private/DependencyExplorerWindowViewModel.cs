using System.Collections.ObjectModel;
using System.Windows;
using Dependinator.ModelViewing.ModelHandling.Core;
using Dependinator.Utils.UI.Mvvm;


namespace Dependinator.ModelViewing.DependencyExploring.Private
{
	internal class DependencyExplorerWindowViewModel : ViewModel, IItemCommands
	{
		private readonly IDependencyWindowService dependencyWindowService;

		public NodeName sourceNodeName;
		public NodeName targetNodeName;


		public DependencyExplorerWindowViewModel(
			IDependencyWindowService dependencyWindowService,
			Node node,
			Line line)
		{
			this.dependencyWindowService = dependencyWindowService;

			dependencyWindowService.Initialize(this, node, line);
		}


		public string SourceText { get => Get(); set => Set(value); }

		public string TargetText { get => Get(); set => Set(value); }
		public string SourceTargetToolTip { get => Get(); set => Set(value); }


		public Command<Window> CancelCommand => Command<Window>(w => w.Close());
		public Command SwitchSidesCommand => AsyncCommand(() => dependencyWindowService.SwitchSidesAsync(this));

		public Command RefreshCommand => AsyncCommand(() => dependencyWindowService.RefreshAsync(this));


		public ObservableCollection<DependencyItemViewModel> SourceItems { get; } =
			new ObservableCollection<DependencyItemViewModel>();

		public ObservableCollection<DependencyItemViewModel> TargetItems { get; } =
			new ObservableCollection<DependencyItemViewModel>();


		public void ShowCode(NodeName nodeName) => dependencyWindowService.ShowCode(nodeName);

		public void FilterOn(DependencyItem item, bool isSourceItem) =>
			dependencyWindowService.FilterOn(this, item, isSourceItem);
	}
}