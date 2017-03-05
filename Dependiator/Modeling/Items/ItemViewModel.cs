using Dependiator.Utils.UI;


namespace Dependiator.Modeling.Items
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
		public double CanvasWidth => item.ItemBounds.Width;
		public double CanvasTop => item.ItemBounds.Top;
		public double CanvasLeft => item.ItemBounds.Left;
		public double CanvasHeight => item.ItemBounds.Height;
	}
}