using System.Windows;
using Dependinator.Utils.UI;

namespace Dependinator.ModelViewing.Private.Items
{
	internal abstract class ItemViewModel : ViewModel, IItem, IItemsCanvasBounds
	{
		// UI properties
		public string Type => this.GetType().Name;

		public double CanvasWidth => ItemBounds.Width;
		public double CanvasTop => ItemBounds.Top;
		public double CanvasLeft => ItemBounds.Left;
		public virtual double CanvasHeight => ItemBounds.Height;

		public virtual Rect ItemBounds { get; set; }

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