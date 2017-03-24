using System.Threading;
using System.Windows;
using Dependiator.Utils;
using Dependiator.Utils.UI;


namespace Dependiator.Modeling.Items
{
	internal abstract class ItemViewModel : ViewModel, IItem
	{
		public static int ItemsCount = 0;

		public int InstanceCount = 0;

		// UI properties
		public string Type => this.GetType().Name;

		public double CanvasWidth => ItemBounds.Width;
		public double CanvasTop => ItemBounds.Top;
		public double CanvasLeft => ItemBounds.Left;
		public double CanvasHeight => ItemBounds.Height;

		public Rect ItemBounds => GetItemBounds();
		public double ItemsScaleFactor => GetScaleFactor();

		public double Priority { get; }
		public ViewModel ViewModel => this;
		public object ItemState { get; set; }


		public bool IsVisible { get; set; }

		public virtual void ItemRealized()
		{
			ItemsCount++;
			InstanceCount++;

			Log.Debug($"{GetType()} {this}, count {ItemsCount} (instance {InstanceCount})");
		}


		public virtual void ItemVirtualized()
		{
			ItemsCount--;
			InstanceCount--;
			Log.Debug($"{GetType()} {this}, count {ItemsCount} (instance {InstanceCount})");
		}


		public abstract Rect GetItemBounds();
		public abstract double GetScaleFactor();

	}
}