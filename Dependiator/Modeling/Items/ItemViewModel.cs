using System.Windows;
using Dependiator.Utils.UI;


namespace Dependiator.Modeling.Items
{
	internal abstract class ItemViewModel : ViewModel, IItem
	{
		// UI properties
		public string Type => this.GetType().Name;

		public double CanvasWidth => ItemBounds.Width;
		public double CanvasTop => ItemBounds.Top;
		public double CanvasLeft => ItemBounds.Left;
		public double CanvasHeight => ItemBounds.Height;

		public Rect ItemBounds => GetItemBounds();

		public double Priority { get; }
		public ViewModel ViewModel => this;
		public object ItemState { get; set; }

		public bool IsShown { get; private set; }

		public void ItemRealized()
		{
			IsShown = true;
		}


		public void ItemVirtualized()
		{
			IsShown = false;
		}


		public abstract Rect GetItemBounds();
	}
}