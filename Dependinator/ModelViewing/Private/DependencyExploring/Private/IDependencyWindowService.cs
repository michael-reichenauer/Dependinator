using System.Threading.Tasks;
using Dependinator.ModelViewing.Private.ModelHandling.Core;


namespace Dependinator.ModelViewing.Private.DependencyExploring.Private
{
    internal interface IDependencyWindowService
    {
        void Initialize(DependencyExplorerWindowViewModel viewModel, Node node, Line line);

        Task SwitchSidesAsync(DependencyExplorerWindowViewModel viewModel);

        void FilterOn(
            DependencyExplorerWindowViewModel viewModel,
            DependencyItem dependencyItem,
            bool isSourceSide);


        Task ShowCodeAsync(NodeName nodeName);

        Task Refresh(DependencyExplorerWindowViewModel viewModel);
        void Locate(NodeName nodeName);
        void ShowDependencyExplorer(NodeName nodeName);
        void HideDependencies(DependencyExplorerWindowViewModel viewModel, NodeName nodeName, bool isSourceItem);


        void ShowDependencies(
            DependencyExplorerWindowViewModel viewModel, NodeName nodeName, bool isSourceItem);
    }
}
