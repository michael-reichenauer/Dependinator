using System.Threading.Tasks;
using Dependiator.Modeling.Items;
using Dependiator.Utils.UI;
using Dependiator.Utils.UI.VirtualCanvas;


namespace Dependiator.Modeling
{
	internal class NodesViewModel : ViewModel
	{
		public ItemsCanvas ItemsCanvas { get; } = new ItemsCanvas();


		public void SetCanvas(ZoomableCanvas zoomableCanvas)
		{
			ItemsCanvas.SetCanvas(zoomableCanvas);
		}


		public Task LoadAsync()
		{
			return Task.CompletedTask;
		}
	}
}