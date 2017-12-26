using System.Windows;
using Dependinator.Utils.UI.Mvvm;


namespace Dependinator.ModelViewing.Items
{
	internal abstract class ItemViewModel : ViewModel, IItem, IItemsCanvasBounds
	{
		protected ItemViewModel()
		{
			ViewName = this.GetType().Name;
		}

		public string ViewName { get => Get(); set => Set(value); }

		public double ItemZIndex { get => Get(); set => Set(value); }


		public double Priority { get; set; }

		public virtual double ItemTop => ItemBounds.Top;
		public virtual double ItemLeft => ItemBounds.Left;
		public virtual double ItemWidth => ItemBounds.Width;
		public virtual double ItemHeight => ItemBounds.Height;


		public Rect ItemBounds
		{
			get => Get();
			set => Set(value)
				.Notify(nameof(ItemTop), nameof(ItemLeft), nameof(ItemWidth), nameof(ItemHeight));
		}

		public object ItemState { get; set; }



		public ItemsCanvas ItemOwnerCanvas { get; set; }
		public double ItemScale => ItemOwnerCanvas?.Scale ?? 1;

		public virtual bool CanShow { get; } = true;

		public bool IsShowing { get; private set; }

		public virtual void ItemRealized() => IsShowing = true;

		public virtual void ItemVirtualized() => IsShowing = false;


		public virtual void MoveItem(Vector moveOffset)
		{
		}
	}
}