using Dependinator.ModelViewing.DataHandling.Dtos;


namespace Dependinator.ModelViewing.DependencyExploring.Private
{
	internal interface IItemCommands
	{
		void ShowCode(NodeName nodeName);

		void FilterOn(DependencyItem item, bool isSourceItem);
	}
}