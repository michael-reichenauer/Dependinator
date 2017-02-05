using System.Windows;
using Dependiator.Utils.UI;


namespace Dependiator.MainViews.Private
{
	internal interface IItem
	{
		Rect ItemCanvasBounds { get; }
		double ZIndex { get; }
		double Priority { get; }
		ViewModel ViewModel { get; }
		object ItemState { get; set; }

		void ItemRealized();
		void ItemVirtualized();
	}
}