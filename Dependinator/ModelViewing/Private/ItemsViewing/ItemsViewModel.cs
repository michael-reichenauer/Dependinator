using System.Windows;
using Dependinator.Utils.UI.Mvvm;
using Dependinator.Utils.UI.VirtualCanvas;


namespace Dependinator.ModelViewing.Private.ItemsViewing
{
	internal class ItemsViewModel : ViewModel
	{
		private readonly ISelectableItem item;


		public ItemsViewModel(
			ItemsCanvas itemsCanvas,
			ISelectableItem item)
		{
			this.item = item;
			ItemsCanvas = itemsCanvas;
		}



		public ItemsCanvas ItemsCanvas { get; }

		public bool IsRoot => ItemsCanvas.IsRoot;


		public void SetZoomableCanvas(ZoomableCanvas zoomableCanvas) =>
			ItemsCanvas.SetZoomableCanvas(zoomableCanvas);


		public void MoveCanvas(Vector viewOffset) => ItemsCanvas.MoveAllItems(viewOffset);

		public void Zoom(double zoomFactor, Point viewPosition) => ItemsCanvas.ZoomNode(zoomFactor, viewPosition);

		public void SizeChanged() => ItemsCanvas.SizeChanged();

		public void ItemRealized() => ItemsCanvas.CanvasRealized();

		public void ItemVirtualized() => ItemsCanvas.CanvasVirtualized();


		public void MoveAllItems(Point p1, Point p2)
		{
			if (item?.IsSelected ?? true)
			{
				ItemsCanvas.MoveAllItems(p1, p2);
			}
			else
			{
				ItemsCanvas.RootCanvas.MoveAllItems(p1, p2);
			}
		}
	}
}