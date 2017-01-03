using System.Windows;
using Dependiator.Utils.UI;


namespace Dependiator.MainViews.Private
{
	internal interface IItem
	{
		object ItemState { get; set; }

		ViewModel ViewModel { get; }

		Rect ItemBounds { get; }

		double Priority { get; }

		int ZIndex { get; }

		bool IsAdded { get; }

		void ChangedScale();
		void ItemRealized();
		void ItemVirtualized();
	}
}