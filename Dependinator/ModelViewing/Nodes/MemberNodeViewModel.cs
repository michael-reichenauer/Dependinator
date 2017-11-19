using Dependinator.ModelHandling;
using Dependinator.ModelHandling.Core;


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