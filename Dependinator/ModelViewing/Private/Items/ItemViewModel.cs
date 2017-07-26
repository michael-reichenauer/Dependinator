using System.Windows;
using Dependinator.Utils.UI;

namespace Dependinator.ModelViewing.Private.Items
{
	internal abstract class ItemViewModel : ViewModel, IItem, IItemsCanvasBounds
	{
		private Rect itemBounds;

		// UI properties
		public string Type => this.GetType().Name;

		public double ItemTop => ItemBounds.Top;
		public double ItemLeft => ItemBounds.Left;
		public double ItemWidth => ItemBounds.Width;
		public double ItemHeight => ItemBounds.Height;

		public Rect ItemBounds
		{
			get => itemBounds;
			set
			{
				itemBounds = value; 
				Notify(nameof(ItemTop), nameof(ItemLeft), nameof(ItemWidth), nameof(ItemHeight));				
			}
		}

		public ViewModel ViewModel => this;
		public object ItemState { get; set; }
		public IItemsCanvas ItemsCanvas { get; set; }
		public double ItemScale => ItemsCanvas.Scale;

		public virtual bool CanShow { get; } = true;

		public bool IsShowing { get; private set; }

		//public void Hide() => CanShow = false;

		//public void Show() => CanShow = true;

		public virtual void ItemRealized() => IsShowing = true;

		public virtual void ItemVirtualized() => IsShowing = false;
	}
}