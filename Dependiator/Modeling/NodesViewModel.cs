using System.Collections.Generic;
using System.Threading.Tasks;
using System.Windows;
using Dependiator.Modeling.Items;
using Dependiator.Utils.UI;
using Dependiator.Utils.UI.VirtualCanvas;


namespace Dependiator.Modeling
{
	internal class NodesViewModel : ViewModel
	{
		private readonly ItemsCanvas itemsCanvas;


		public NodesViewModel(ItemsCanvas itemsCanvas)
		{
			this.itemsCanvas = itemsCanvas;
		}


		public void SetCanvas(ZoomableCanvas zoomableCanvas, NodesView nodeView)
		{
			itemsCanvas.SetCanvas(zoomableCanvas);
			NodeView = nodeView;
		}


		public NodesView NodeView { get; private set; }





		public void MoveCanvas(Vector viewOffset) => itemsCanvas.Offset -= viewOffset;


		public void SizeChanged() => itemsCanvas.TriggerExtentChanged();


		public void AddItem(IItem item) => itemsCanvas.AddItem(item);

		public void AddItems(IEnumerable<IItem> items) => itemsCanvas.AddItems(items);


		public void RemoveItem(IItem item) => itemsCanvas.RemoveItem(item);


		public void UpdateItem(IItem item) => itemsCanvas.UpdateItem(item);
	}
}