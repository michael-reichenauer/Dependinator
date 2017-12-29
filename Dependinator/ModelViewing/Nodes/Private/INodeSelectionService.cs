using Dependinator.ModelViewing.ModelHandling.Core;


namespace Dependinator.ModelViewing.Nodes.Private
{
	internal interface INodeSelectionService
	{
		bool IsRootSelected { get; }
		void Clicked(NodeViewModel node);
	}
}