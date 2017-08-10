using Dependinator.Modeling;

namespace Dependinator.ModelViewing.Nodes
{
	internal class MemberNodeViewModel : NodeViewModel
	{
		public MemberNodeViewModel(INodeViewModelService nodeViewModelService, Node node)
			: base(nodeViewModelService, node)
		{
			ViewName = nameof(MemberNodeView);
		}
	}
}