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
		public NodesViewModel(ItemsCanvas itemsCanvas)
		{
			ItemsCanvas = itemsCanvas;
		}


		public void SetCanvas(ZoomableCanvas zoomableCanvas, NodesView nodeView)
		{
			ItemsCanvas.SetCanvas(zoomableCanvas);
			NodeView = nodeView;
		}


		public NodesView NodeView { get; private set; }


		public ItemsCanvas ItemsCanvas { get; }


		public Task LoadAsync()
		{
			return Task.CompletedTask;
		}



		public void MoveCanvas(Vector viewOffset) => ItemsCanvas.Offset -= viewOffset;


		public void SizeChanged() => ItemsCanvas.TriggerExtentChanged();


		public void AddItem(IItem item) => ItemsCanvas.AddItem(item);

		public void AddItems(IEnumerable<IItem> items) => ItemsCanvas.AddItems(items);


		public void RemoveItem(IItem item) => ItemsCanvas.RemoveItem(item);


		public void UpdateItem(IItem item) => ItemsCanvas.UpdateItem(item);
	}
}