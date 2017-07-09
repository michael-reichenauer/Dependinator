using Dependinator.ModelViewing.Private.Items;
using Dependinator.ModelViewing.Private.Items.Private;


namespace Dependinator.ModelViewing.Nodes
{
	internal class TypeViewModel : CompositeNodeViewModel
	{
		public TypeViewModel(IItemsService itemsService, Node node, IItemsCanvas itemsCanvas)
			: base(itemsService, node, itemsCanvas)
		{
		}
	}
}