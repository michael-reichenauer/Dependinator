using System;
using System.Windows;
using Dependiator.Utils.UI;


namespace Dependiator.MainViews.Private
{
	internal abstract class Item : IItem
	{
		private readonly Lazy<ItemViewModel> viewModel;

		protected Item()
		{
			viewModel = new Lazy<ItemViewModel>(ViewModelFactory);
		}

		public object ItemState { get; set; }
		public bool IsAdded => ItemState != null;
		public ViewModel ViewModel => viewModel.Value;

		public Rect ItemBounds { get; protected set; }
		public int ZIndex { get; protected set; }
		public double Priority { get; protected set; }

		public bool IsRealized { get; private set; }

		public abstract ItemViewModel ViewModelFactory();

		public virtual void ChangedScale()
		{
			NotifyAll();
		}

		public void NotifyAll()
		{
			if (IsAdded)
			{
				ViewModel.NotifyAll();
			}
		}


		public virtual void ItemRealized()
		{
			IsRealized = true;
		}


		public virtual void ItemVirtualized()
		{
			IsRealized = false;
		}
	}
}