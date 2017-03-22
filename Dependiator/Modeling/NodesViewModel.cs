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
		private readonly ItemsCanvas canvas;


		public NodesViewModel(IItem item, ItemsCanvas parentCanvas)
		{
			canvas = new ItemsCanvas(item, parentCanvas);
		}


		public void SetCanvas(ZoomableCanvas zoomableCanvas)
		{
			canvas.SetCanvas(zoomableCanvas);
		}



		public double Scale
		{
			get { return canvas.Scale; }
			set { canvas.Scale = value; }
		}

		public ItemsCanvas ItemsCanvas => canvas;


		public Task LoadAsync()
		{
			return Task.CompletedTask;
		}


		public void Zoom(int zoomDelta, Point viewPosition) => canvas.Zoom(zoomDelta, viewPosition);


		public void MoveCanvas(Vector viewOffset) => canvas.Offset -= viewOffset;


		public void SizeChanged() => canvas.TriggerExtentChanged();


		public void AddItem(IItem item) => canvas.AddItem(item);

		public void AddItems(IEnumerable<IItem> items) => canvas.AddItems(items);


		public void RemoveItem(IItem item) => canvas.RemoveItem(item);


		public void UpdateItem(IItem item) => canvas.UpdateItem(item);
	}
}