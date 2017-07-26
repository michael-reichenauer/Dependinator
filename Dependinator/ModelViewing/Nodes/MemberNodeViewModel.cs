using Dependinator.Modeling;

namespace Dependinator.ModelViewing.Nodes
{
	internal class MemberNodeViewModel : NodeViewModel
	{
		public MemberNodeViewModel(INodeService nodeService, Node node)
			: base(nodeService, node)
		{
			ViewName = nameof(MemberNodeView);
		}
	}
}