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

		public ViewModel ViewModel => this;
		public object ItemState { get; set; }


		public bool CanShow { get; private set; }
		public bool IsShowing { get; private set; }


		public void Hide()
		{
			CanShow = false;
		}

		public void Show()
		{
			CanShow = true;
		}


		public virtual void ItemRealized()
		{
			Log.Debug($"{GetType()} {this}");

			if (!IsShowing)
			{
				IsShowing = true;
				TotalCount++;
			}
		}


		public virtual void ItemVirtualized()
		{
			Log.Debug($"{GetType()} {this}");

			if (IsShowing)
			{
				IsShowing = false;
				TotalCount--;
			}
		}


		public virtual void SetParentVirtualized()
		{
			if (!IsShowing)
			{
				return;
			}

			Log.Debug($"{GetType()} {this}");

			IsShowing = false;
			TotalCount--;
		}


		protected abstract Rect GetItemBounds();
	}
}