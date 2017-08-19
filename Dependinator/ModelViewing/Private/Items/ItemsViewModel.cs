using System.Windows;
using Dependinator.Utils.UI;
using Dependinator.Utils.UI.VirtualCanvas;

namespace Dependinator.ModelViewing.Private.Items
{
	internal class ItemsViewModel : ViewModel
	{
		public ItemsViewModel(ItemsCanvas itemsCanvas)
		{
			ItemsCanvas = itemsCanvas;
		}

		public ItemsCanvas ItemsCanvas { get; }

		public bool IsRoot => ItemsCanvas.IsRoot;

		public void SetZoomableCanvas(ZoomableCanvas zoomableCanvas) =>
			ItemsCanvas.SetZoomableCanvas(zoomableCanvas);

		public void MoveCanvas(Vector viewOffset) => ItemsCanvas.Move(viewOffset);

		public void ZoomRoot(double zoom, Point viewPosition) => ItemsCanvas.CanvasRoot.Zoom(zoom, viewPosition);

		public void Zoom(double zoom, Point viewPosition) => ItemsCanvas.Zoom(zoom, viewPosition);

		public void SizeChanged() => ItemsCanvas.SizeChanged();

		public void ItemRealized() => ItemsCanvas.ItemRealized();

		public void ItemVirtualized() => ItemsCanvas.ItemVirtualized();

	}
}