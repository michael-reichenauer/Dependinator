using System.Windows;
using Dependinator.ModelViewing.Private.Nodes;
using Dependinator.Utils.UI.Mvvm;
using Dependinator.Utils.UI.VirtualCanvas;


namespace Dependinator.ModelViewing.Private.Items
{
	internal class ItemsViewModel : ViewModel
	{
		private readonly NodeViewModel node;


		public ItemsViewModel(
			ItemsCanvas itemsCanvas,
			NodeViewModel node)
		{
			this.node = node;
			ItemsCanvas = itemsCanvas;
		}


		public ItemsCanvas ItemsCanvas { get; }

		public bool IsRoot => ItemsCanvas.IsRoot;


		public void SetZoomableCanvas(ZoomableCanvas zoomableCanvas) =>
			ItemsCanvas.SetZoomableCanvas(zoomableCanvas);


		public void MoveCanvas(Vector viewOffset) => ItemsCanvas.MoveAllItems(viewOffset);

		public void Zoom(double zoom, Point viewPosition) => ItemsCanvas.ZoomNode(zoom, viewPosition);

		public void SizeChanged() => ItemsCanvas.SizeChanged();

		public void ItemRealized() => ItemsCanvas.CanvasRealized();

		public void ItemVirtualized() => ItemsCanvas.CanvasVirtualized();


		public void MoveAllItems(Point p1, Point p2)
		{
			if (node?.IsSelected ?? true)
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