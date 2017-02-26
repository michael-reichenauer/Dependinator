using System.Windows;
using Dependiator.Utils.UI;


namespace Dependiator.MainViews.Private
{
	internal interface IItem
	{
		// !!! Ta bort :::
		double ZIndex { get; }


		Rect ItemCanvasBounds { get; }
		
		double Priority { get; }
		ViewModel ViewModel { get; }
		object ItemState { get; set; }

		void ItemRealized();
		void ItemVirtualized();
	}
}