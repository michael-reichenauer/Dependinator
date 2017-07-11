using System.Windows;
using Dependinator.ModelViewing.Private.Items;
using Dependinator.ModelViewing.Private.Items.Private;


namespace Dependinator.ModelViewing.Nodes
{
	internal class CompositeNodeViewModel : NodeOldViewModel
	{
		private readonly NodeOld node;


		public CompositeNodeViewModel(IItemsService itemsService, NodeOld node, IItemsCanvas itemsCanvas)
			: base(node)
		{
			this.node = node;
			ItemsCanvas = itemsCanvas;
			ItemsViewModel = new ItemsViewModel(itemsService, node, ItemsCanvas);
		}



		public ItemsViewModel ItemsViewModel { get; }

		public IItemsCanvas ItemsCanvas { get; }

		public double Scale => ItemsCanvas.Scale;



		public override void ItemRealized()
		{
			base.ItemRealized();
			node.NodeRealized();
		}


		public override void ItemVirtualized()
		{
			node.NodeVirtualized();
			base.ItemVirtualized();
		}


		public void UpdateScale()
		{
			ItemsCanvas.UpdateScale();
			NotifyAll();
		}



		public void Zoom(double zoomFactor, Point viewPosition) => node.Zoom(zoomFactor, viewPosition);

		public void ZoomResize(int wheelDelta) => node.Resize(wheelDelta);



		public void ResizeeNode(Vector viewOffset, Point viewPosition2) => node.Resize(viewOffset, viewPosition2);

	}
}