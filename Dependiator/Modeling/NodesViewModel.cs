using System.Collections.Generic;
using System.Windows;
using Dependiator.Modeling.Items;
using Dependiator.Utils.UI;
using Dependiator.Utils.UI.VirtualCanvas;


namespace Dependiator.Modeling
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


		public void SetCanvas(ZoomableCanvas zoomableCanvas)
		{
			itemsCanvas.SetCanvas(zoomableCanvas);
		}



		public void MoveCanvas(Vector viewOffset) => node?.MoveItems(viewOffset);


		public void SizeChanged() => itemsCanvas.TriggerExtentChanged();


		public void AddItem(IItem item) => itemsCanvas.AddItem(item);

		public void AddItems(IEnumerable<IItem> items) => itemsCanvas.AddItems(items);


		public void RemoveItem(IItem item) => itemsCanvas.RemoveItem(item);


		public void UpdateItem(IItem item) => itemsCanvas.UpdateItem(item);
	}
}