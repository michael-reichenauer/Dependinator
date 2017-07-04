using System.Windows;
using Dependinator.ModelViewing.Items;
using Dependinator.ModelViewing.Nodes;
using Dependinator.ModelViewing.Private;
using Dependinator.Utils.UI;
using Dependinator.Utils.UI.VirtualCanvas;

namespace Dependinator.ModelViewing
{
	internal class ModelViewModel : ViewModel
	{
		private readonly IModelService modelService;
		private readonly Node node;
		private readonly ItemsCanvas itemsCanvas;


		public ModelViewModel(IModelService modelService, Node node, ItemsCanvas itemsCanvas)
		{
			this.modelService = modelService;
			this.node = node;
			this.itemsCanvas = itemsCanvas;
		}

		public bool IsRoot => node == null;


		public void SetCanvas(ZoomableCanvas zoomableCanvas, ModelView modelView)
		{
			itemsCanvas.SetCanvas(zoomableCanvas, modelView);
		}


		public void MoveCanvas(Vector viewOffset) => modelService.Move(node, viewOffset);


		public void SizeChanged() => itemsCanvas.TriggerExtentChanged();

		public void ZoomRoot(double zoom, Point viewPosition) => modelService.Zoom(zoom, viewPosition);
	}
}