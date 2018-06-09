using Dependinator.ModelViewing.ModelHandling.Core;


namespace Dependinator.ModelViewing.Nodes
{
	internal interface INodeLayoutService
	{
		void SetLayout(NodeViewModel node);
		void ResetLayout(Node node);
	}
}