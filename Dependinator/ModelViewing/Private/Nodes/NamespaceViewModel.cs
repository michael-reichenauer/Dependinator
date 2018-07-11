using Dependinator.ModelViewing.Private.ModelHandling.Core;


namespace Dependinator.ModelViewing.Private.Nodes
{
	internal class NamespaceViewModel : NodeViewModel
	{
		public NamespaceViewModel(INodeViewModelService nodeViewModelService, Node node)
			: base(nodeViewModelService, node)
		{
			ViewName = nameof(NamespaceView);
		}
	}
}