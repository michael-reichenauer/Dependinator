namespace Dependinator.ModelViewing.Nodes
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