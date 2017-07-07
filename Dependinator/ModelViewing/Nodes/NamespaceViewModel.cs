using Dependinator.ModelViewing.Private;
using Dependinator.ModelViewing.Private.Items;

namespace Dependinator.ModelViewing.Nodes
{
	internal class NamespaceViewModel : CompositeNodeViewModel
	{
		public NamespaceViewModel(IItemsService itemsService, Node node, ItemsCanvas itemsCanvas)
			: base(itemsService, node, itemsCanvas)
		{
		}
	}
}