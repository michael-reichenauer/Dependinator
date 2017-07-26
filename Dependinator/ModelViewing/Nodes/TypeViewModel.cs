using Dependinator.Modeling;


namespace Dependinator.ModelViewing.Nodes
{
	internal class TypeViewModel : CompositeNodeViewModel
	{
		public TypeViewModel(INodeService nodeService, Node node)
			: base(nodeService, node)
		{
			ViewName = nameof(TypeView);
		}
	}
}