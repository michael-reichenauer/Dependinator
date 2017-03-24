using System.Threading;
using System.Windows;
using Dependiator.Utils;
using Dependiator.Utils.UI;


namespace Dependiator.Modeling.Items
{
	internal abstract class ItemViewModel : ViewModel, IItem
	{
		public static int TotalCount = 0;

		public int InstanceCount = 0;

		// UI properties
		public string Type => this.GetType().Name;

		public double CanvasWidth => ItemBounds.Width;
		public double CanvasTop => ItemBounds.Top;
		public double CanvasLeft => ItemBounds.Left;
		public double CanvasHeight => ItemBounds.Height;

		public Rect ItemBounds => GetItemBounds();
		public double ItemsScaleFactor => GetScaleFactor();

		public ViewModel ViewModel => this;
		public object ItemState { get; set; }


		public bool IsShowEnabled { get; private set; }
		public bool IsShowing { get; private set; }


		public void Hide()
		{
			IsShowEnabled = false;
		}

		public void Show()
		{
			IsShowEnabled = true;
		}


		public virtual void ItemRealized()
		{
			TotalCount++;
			InstanceCount++;
			Log.Debug($"{GetType()} {this}, Total: {TotalCount}, Instance: {InstanceCount}");

			if (IsShowing)
			{
				Log.Warn("Item already realized");
			}

			IsShowing = true;			
		}


		public virtual void ItemVirtualized()
		{
			TotalCount--;
			InstanceCount--;
			Log.Debug($"{GetType()} {this}, Total: {TotalCount}, Instance: {InstanceCount}");

			if (!IsShowing)
			{
				Log.Warn("Item already virtualized");
			}

			IsShowing = false;
		}


		public abstract Rect GetItemBounds();
		public abstract double GetScaleFactor();

	}
}