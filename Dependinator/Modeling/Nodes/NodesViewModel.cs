using System.Collections.Generic;
using System.Windows;
using Dependinator.Modeling.Items;
using Dependinator.Utils.UI;
using Dependinator.Utils.UI.VirtualCanvas;


namespace Dependinator.Modeling.Nodes
{
	internal class NodesViewModel : ViewModel
	{
		private readonly Node node;
		private readonly ItemsCanvas itemsCanvas;


		public NodesViewModel(Node node, ItemsCanvas itemsCanvas)
		{
			this.node = node;
			this.itemsCanvas = itemsCanvas;
		}


		public void SetCanvas(ZoomableCanvas zoomableCanvas, NodesView nodesView)
		{
			itemsCanvas.SetCanvas(zoomableCanvas, nodesView);
		}
		

		public void MoveCanvas(Vector viewOffset) => node?.MoveItems(viewOffset);


		public void SizeChanged() => itemsCanvas.TriggerExtentChanged();
	}
}