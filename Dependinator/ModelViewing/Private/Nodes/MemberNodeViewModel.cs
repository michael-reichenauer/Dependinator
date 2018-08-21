using Dependinator.ModelViewing.Private.ModelHandling.Core;


namespace Dependinator.ModelViewing.Private.Nodes
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
