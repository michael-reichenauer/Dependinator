using System.Windows;
using Dependinator.ModelViewing.Items;
using Dependinator.Utils.UI;
using Dependinator.Utils.UI.VirtualCanvas;


namespace Dependinator.ModelViewing.Nodes
{
	internal class ModelViewModel : ViewModel
	{
		private readonly IModelViewService modelViewService;
		private readonly Node node;
		private readonly ItemsCanvas itemsCanvas;


		public ModelViewModel(IModelViewService modelViewService, Node node, ItemsCanvas itemsCanvas)
		{
			this.modelViewService = modelViewService;
			this.node = node;
			this.itemsCanvas = itemsCanvas;
		}

		public bool IsRoot => node == null;


		public void SetCanvas(ZoomableCanvas zoomableCanvas, ModelView modelView)
		{
			itemsCanvas.SetCanvas(zoomableCanvas, modelView);
		}


		public void MoveCanvas(Vector viewOffset)
		{
			if (node != null)
			{
				node.MoveItems(viewOffset);
			}
			else
			{
				modelViewService.Move(viewOffset);
			}
		}


		public void SizeChanged() => itemsCanvas.TriggerExtentChanged();

		public void ZoomRoot(double zoom, Point viewPosition) => modelViewService.Zoom(zoom, viewPosition);
	}
}