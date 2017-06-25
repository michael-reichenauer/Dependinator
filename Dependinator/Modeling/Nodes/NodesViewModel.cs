using System.Collections.Generic;
using System.Windows;
using Dependinator.MainViews.Private;
using Dependinator.Modeling.Items;
using Dependinator.Utils.UI;
using Dependinator.Utils.UI.VirtualCanvas;


namespace Dependinator.Modeling.Nodes
{
	internal class NodesViewModel : ViewModel
	{
		private readonly IModelViewService modelViewService;
		private readonly Node node;
		private readonly ItemsCanvas itemsCanvas;


		public NodesViewModel(IModelViewService modelViewService, Node node, ItemsCanvas itemsCanvas)
		{
			this.modelViewService = modelViewService;
			this.node = node;
			this.itemsCanvas = itemsCanvas;
		}

		public bool IsRoot => node == null;


		public void SetCanvas(ZoomableCanvas zoomableCanvas, NodesView nodesView)
		{
			itemsCanvas.SetCanvas(zoomableCanvas, nodesView);
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