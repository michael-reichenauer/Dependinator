using System.Threading.Tasks;
using Dependinator.ModelViewing.DataHandling.Dtos;
using Dependinator.ModelViewing.ModelHandling.Core;
using Dependinator.ModelViewing.Nodes;


namespace Dependinator.ModelViewing.DependencyExploring.Private
{
	internal interface IDependencyWindowService
	{
		void Initialize(DependencyExplorerWindowViewModel viewModel, Node node, Line line);

		Task SwitchSidesAsync(DependencyExplorerWindowViewModel viewModel);

		Task RefreshAsync(DependencyExplorerWindowViewModel viewModel);

		void FilterOn(
			DependencyExplorerWindowViewModel viewModel,
			DependencyItem dependencyItem,
			bool isSourceSide);

		void ShowCode(NodeName nodeName);

		Task Refresh(DependencyExplorerWindowViewModel viewModel);
		void Locate(NodeName nodeName);
		void ShowDependencies(NodeName nodeName);
	}
}