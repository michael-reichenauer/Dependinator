using Dependinator.ModelViewing.ModelHandling.Core;


namespace Dependinator.ModelViewing.DependencyExploring
{
	internal interface IDependencyExplorerService
	{
		void ShowWindow(Node node);
		void ShowWindow(Line line);

	}
}