using Dependinator.ModelViewing.Items;
using Dependinator.ModelViewing.Private;


namespace Dependinator.ModelViewing.Nodes
{
	internal class TypeViewModel : CompositeNodeViewModel
	{
		public TypeViewModel(IModelService modelService, Node node, ItemsCanvas itemsCanvas)
			: base(modelService, node, itemsCanvas)
		{
		}
	}
}