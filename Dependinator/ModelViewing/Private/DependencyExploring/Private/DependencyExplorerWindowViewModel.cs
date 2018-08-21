using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using Dependinator.ModelViewing.Private.ModelHandling.Core;
using Dependinator.Utils.UI.Mvvm;


namespace Dependinator.ModelViewing.Private.DependencyExploring.Private
{
    internal class DependencyExplorerWindowViewModel : ViewModel, IItemCommands
    {
        private readonly IDependencyWindowService dependencyWindowService;


        public DependencyExplorerWindowViewModel(
            IDependencyWindowService dependencyWindowService,
            Node node,
            Line line)
        {
            this.dependencyWindowService = dependencyWindowService;

            dependencyWindowService.Initialize(this, node, line);
        }


        public bool HasHiddenDependencies => HasHiddenSourceNodes || HasHiddenTargetNodes;
        public bool HasHiddenSourceNodes => HiddenSourceNodes.Any();
        public bool HasHiddenTargetNodes => HiddenTargetNodes.Any();

        public IReadOnlyList<HiddenNodeItem> HiddenSourceItems => GetHiddenSourceItems();
        public IReadOnlyList<HiddenNodeItem> HiddenTargetItems => GetHiddenTargetItems();


        public NodeName SourceNodeName { get; set; }

        public NodeName TargetNodeName { get; set; }

        public string SourceText { get => Get(); set => Set(value); }

        public string TargetText { get => Get(); set => Set(value); }

        public List<Node> HiddenSourceNodes { get; } = new List<Node>();
        public List<Node> HiddenTargetNodes { get; } = new List<Node>();

        public Command<Window> CancelCommand => Command<Window>(w => w.Close());

        public Command SwitchSidesCommand => AsyncCommand(() => dependencyWindowService.SwitchSidesAsync(this));


        public ObservableCollection<DependencyItemViewModel> SourceItems { get; } =
            new ObservableCollection<DependencyItemViewModel>();

        public ObservableCollection<DependencyItemViewModel> TargetItems { get; } =
            new ObservableCollection<DependencyItemViewModel>();


        public Task ShowCodeAsync(NodeName nodeName) => dependencyWindowService.ShowCodeAsync(nodeName);
        public void Locate(NodeName nodeName) => dependencyWindowService.Locate(nodeName);

        public void ShowDependencyExplorer(NodeName nodeName)
            => dependencyWindowService.ShowDependencyExplorer(nodeName);

        public void HideDependencies(NodeName nodeName, bool isSourceItem) =>
            dependencyWindowService.HideDependencies(this, nodeName, isSourceItem);



        public void FilterOn(DependencyItem item, bool isSourceItem) =>
            dependencyWindowService.FilterOn(this, item, isSourceItem);


        public void ModelChanged() => dependencyWindowService.Refresh(this);


        private IReadOnlyList<HiddenNodeItem> GetHiddenSourceItems()
            => HiddenSourceNodes.Select(node => new HiddenNodeItem(
                node.Name, name => ShowDependencies(name, true))).ToList();


        private IReadOnlyList<HiddenNodeItem> GetHiddenTargetItems()
            => HiddenTargetNodes.Select(node => new HiddenNodeItem(
                node.Name, name => ShowDependencies(name, false))).ToList();


        private void ShowDependencies(NodeName nodeName, bool isSource)
            => dependencyWindowService.ShowDependencies(this, nodeName, isSource);
    }
}
