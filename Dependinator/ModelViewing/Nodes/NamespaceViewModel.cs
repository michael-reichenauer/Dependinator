using Dependinator.ModelViewing.Items;
using Dependinator.ModelViewing.Private;

namespace Dependinator.ModelViewing.Nodes
{
	internal class NamespaceViewModel : CompositeNodeViewModel
	{
		public NamespaceViewModel(IModelService modelService, Node node, ItemsCanvas itemsCanvas)
			: base(modelService, node, itemsCanvas)
		{
		}
	}
}