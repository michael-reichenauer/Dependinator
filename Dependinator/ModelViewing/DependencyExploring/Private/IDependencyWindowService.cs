using System.Threading.Tasks;
using Dependinator.ModelViewing.ModelHandling.Core;


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
	}
}