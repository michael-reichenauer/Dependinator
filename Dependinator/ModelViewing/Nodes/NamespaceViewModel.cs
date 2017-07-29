using Dependinator.Modeling;

namespace Dependinator.ModelViewing.Nodes
{
	internal class NamespaceViewModel : CompositeNodeViewModel
	{
		public NamespaceViewModel(INodeViewModelService nodeViewModelService, Node node)
			: base(nodeViewModelService, node)
		{
			ViewName = nameof(NamespaceView);
		}
	}
}