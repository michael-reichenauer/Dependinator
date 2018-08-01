using System.Windows;
using Dependinator.ModelViewing.Private.ItemsViewing;
using Dependinator.Utils.UI.Mvvm;


namespace Dependinator.ModelViewing
{
    internal abstract class ItemViewModel : ViewModel, IItem, IItemsCanvasOwner
    {
        protected ItemViewModel()
        {
            ViewName = GetType().Name;
        }


        public string ViewName { get => Get(); set => Set(value); }

        public double ItemZIndex { get => Get(); set => Set(value); }

        public virtual double ItemTop => ItemBounds.Top;
        public virtual double ItemLeft => ItemBounds.Left;
        public virtual double ItemWidth => ItemBounds.Width;
        public virtual double ItemHeight => ItemBounds.Height;
        public double ItemScale => ItemOwnerCanvas?.Scale ?? 1;
        public double ItemParentScale => ItemOwnerCanvas?.ParentCanvas?.Scale ?? ItemScale;


        // State data used by ItemsSource to track this item
        object IItem.ItemState { get; set; }


        public double Priority { get; set; }


        public Rect ItemBounds
        {
            get => Get();
            set => Set(value)
                .Notify(nameof(ItemTop), nameof(ItemLeft), nameof(ItemWidth), nameof(ItemHeight));
        }

        public ItemsCanvas ItemOwnerCanvas { get; set; }

        public virtual bool CanShow { get; } = true;

        public bool IsShowing { get; private set; }

        public virtual void ItemRealized() => IsShowing = true;

        public virtual void ItemVirtualized() => IsShowing = false;


        public virtual void MoveItem(Vector moveOffset)
        {
        }
    }
}
