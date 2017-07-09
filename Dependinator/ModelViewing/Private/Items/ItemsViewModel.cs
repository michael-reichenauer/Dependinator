using System.Windows;
using Dependinator.ModelViewing.Nodes;
using Dependinator.ModelViewing.Private.Items.Private;
using Dependinator.Utils.UI;
using Dependinator.Utils.UI.VirtualCanvas;

namespace Dependinator.ModelViewing.Private.Items
{
	internal class ItemsViewModel : ViewModel
	{
		private readonly IItemsService itemsService;
		private readonly Node node;
		private readonly ItemsCanvas itemsCanvas;


		public ItemsViewModel(IItemsService itemsService, Node node, ItemsCanvas itemsCanvas)
		{
			this.itemsService = itemsService;
			this.node = node;
			this.itemsCanvas = itemsCanvas;
		}

		public bool IsRoot => node == null;


		public void SetCanvas(ZoomableCanvas zoomableCanvas, ItemsView itemsView)
		{
			itemsCanvas.SetCanvas(zoomableCanvas, itemsView);
		}


		public void MoveCanvas(Vector viewOffset) => itemsService.Move(node, viewOffset);


		public void SizeChanged() => itemsCanvas.TriggerExtentChanged();

		public void ZoomRoot(double zoom, Point viewPosition) => itemsService.Zoom(zoom, viewPosition);

		public void Zoom(double zoom, Point viewPosition)
		{
			node.Zoom(zoom, viewPosition);
		}
	}
}