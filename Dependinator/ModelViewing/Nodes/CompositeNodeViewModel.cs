using Dependinator.Modeling;
using Dependinator.ModelViewing.Private.Items;


namespace Dependinator.ModelViewing.Nodes
{
	internal abstract class CompositeNodeViewModel : NodeViewModel
	{
		protected CompositeNodeViewModel(INodeService nodeService, Node node)
			: base(nodeService, node)
		{
		}


		public ItemsViewModel ItemsViewModel { get; set; }


		public override void ItemRealized()
		{
			base.ItemRealized();

			// If this node has an items canvas, make sure it knows it has been realized (fix zoom level)
			ItemsViewModel?.ItemRealized();
		}
	}
}