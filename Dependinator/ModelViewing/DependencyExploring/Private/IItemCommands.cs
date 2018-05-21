using Dependinator.ModelViewing.ModelHandling.Core;


namespace Dependinator.ModelViewing.DependencyExploring.Private
{
	internal interface IItemCommands
	{
		void ShowCode(Node node);
		void FilterOn(DependencyItem item, bool isSourceItem);
	}
}