using System;
using System.Windows;
using Dependiator.MainViews.Private;
using Dependiator.Utils.UI;


namespace Dependiator.MainViews
{
	internal abstract class Item : IItem
	{
		private readonly Lazy<ItemViewModel> viewModel;

		protected Item()
		{
			viewModel = new Lazy<ItemViewModel>(ViewModelFactory);
		}

		public object VirtualId { get; set; }
		public ViewModel ViewModel => viewModel.Value;

		public Rect ItemBounds { get; set; }
		public int ZIndex { get; set; }
		public double Priority { get; set; }

		public abstract void ZoomChanged();

		public abstract ItemViewModel ViewModelFactory();
	}
}