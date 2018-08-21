using Dependinator.ModelViewing.Private.ModelHandling.Core;


namespace Dependinator.ModelViewing.Private.Nodes
{
    internal class TypeViewModel : NodeViewModel
    {
        public TypeViewModel(INodeViewModelService nodeViewModelService, Node node)
            : base(nodeViewModelService, node)
        {
            ViewName = nameof(TypeView);
        }
    }
}
