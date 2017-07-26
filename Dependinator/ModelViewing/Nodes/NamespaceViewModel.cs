using Dependinator.Modeling;

namespace Dependinator.ModelViewing.Nodes
{
	internal class NamespaceViewModel : CompositeNodeViewModel
	{
		public NamespaceViewModel(INodeService nodeService, Node node)
			: base(nodeService, node)
		{
			ViewName = nameof(NamespaceView);
		}
	}
}