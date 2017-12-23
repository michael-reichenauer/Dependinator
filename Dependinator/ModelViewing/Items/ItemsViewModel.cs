using System.Windows;
using Dependinator.ModelViewing.ModelHandling.Core;
using Dependinator.Utils.UI.Mvvm;
using Dependinator.Utils.UI.VirtualCanvas;


namespace Dependinator.ModelViewing.Items
{
	internal class ItemsViewModel : ViewModel
	{
		private readonly Node node;


		public ItemsViewModel(ItemsCanvas itemsCanvas, Node node)
		{
			this.node = node;
			ItemsCanvas = itemsCanvas;
		}


		public ItemsCanvas ItemsCanvas { get; }

		public bool IsRoot => ItemsCanvas.IsRoot;

		public void SetZoomableCanvas(ZoomableCanvas zoomableCanvas) =>
			ItemsCanvas.SetZoomableCanvas(zoomableCanvas);

		public void MoveCanvas(Vector viewOffset) => ItemsCanvas.Move(viewOffset);
		public void MoveRootCanvas(Vector viewOffset) => ItemsCanvas.MoveRootNode(viewOffset);

		public void ZoomRoot(double zoom, Point viewPosition) => ItemsCanvas.ZoomRootNode(zoom, viewPosition);

		public void Zoom(double zoom, Point viewPosition) => ItemsCanvas.ZoomNode(zoom, viewPosition);

		public void SizeChanged() => ItemsCanvas.SizeChanged();

		public void ItemRealized() => ItemsCanvas.ItemRealized();

		public void ItemVirtualized() => ItemsCanvas.ItemVirtualized();


		public void MoveItems(Vector viewOffset)
		{
			if (node?.IsSelected ?? true)
			{
				MoveCanvas(viewOffset);
			}
			else
			{
				MoveRootCanvas(viewOffset);
			}
		}
	}
}