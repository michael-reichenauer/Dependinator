using Dependinator.Modeling;


namespace Dependinator.ModelViewing.Nodes
{
	internal class TypeViewModel : CompositeNodeViewModel
	{
		public TypeViewModel(INodeViewModelService nodeViewModelService, Node node)
			: base(nodeViewModelService, node)
		{
			ViewName = nameof(TypeView);
		}
	}
}