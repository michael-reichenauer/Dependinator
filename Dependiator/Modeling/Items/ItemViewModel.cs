using Dependiator.MainViews.Private;
using Dependiator.Utils.UI;


namespace Dependiator.MainViews
{
	internal class ItemViewModel : ViewModel
	{
		private readonly IItem item;


		public ItemViewModel(IItem item)
		{
			this.item = item;
		}


		// UI properties
		public string Type => this.GetType().Name;
		public double CanvasZIndex => item.ZIndex;
		public double CanvasWidth => item.ItemCanvasBounds.Width;
		public double CanvasTop => item.ItemCanvasBounds.Top;
		public double CanvasLeft => item.ItemCanvasBounds.Left;
		public double CanvasHeight => item.ItemCanvasBounds.Height;
	}
}