using Dependinator.ModelViewing.DataHandling.Dtos;
using Dependinator.ModelViewing.Nodes;


namespace Dependinator.ModelViewing.DependencyExploring.Private
{
	internal interface IItemCommands
	{
		void ShowCode(NodeName nodeName);

		void Locate(NodeName nodeName);

		void FilterOn(DependencyItem item, bool isSourceItem);
		void ShowDependencies(NodeName nodeName);
	}
}