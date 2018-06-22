using Dependinator.ModelViewing.DataHandling;
using Dependinator.ModelViewing.DataHandling.Dtos;
using Dependinator.ModelViewing.ModelHandling.Core;


namespace Dependinator.ModelViewing.DependencyExploring.Private
{
	internal interface IItemCommands
	{
		void ShowCode(NodeName nodeName);

		void FilterOn(DependencyItem item, bool isSourceItem);
	}
}