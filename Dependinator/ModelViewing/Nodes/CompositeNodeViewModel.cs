using System.Windows;
using Dependinator.ModelViewing.Items;
using Dependinator.ModelViewing.Private;


namespace Dependinator.ModelViewing.Nodes
{
	internal class CompositeNodeViewModel : NodeViewModel
	{
		private readonly Node node;


		public CompositeNodeViewModel(IModelService modelService, Node node, ItemsCanvas itemsCanvas)
			: base(node)
		{
			this.node = node;
			ItemsCanvas = itemsCanvas;
			ModelViewModel = new ModelViewModel(modelService, node, ItemsCanvas);
		}



		public ModelViewModel ModelViewModel { get; }

		public ItemsCanvas ItemsCanvas { get; }

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