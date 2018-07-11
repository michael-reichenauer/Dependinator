using Dependinator.ModelViewing.Private.ModelHandling.Core;


namespace Dependinator.ModelViewing.Private.Nodes
{
	internal interface INodeLayoutService
	{
		void SetLayout(NodeViewModel node);
		void ResetLayout(Node node);
	}
}