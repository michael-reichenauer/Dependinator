using System.Windows;
using Dependiator.Utils.UI;


namespace Dependiator.Modeling.Items
{
	internal interface IItem
	{
		Rect ItemBounds { get; }
		double ItemsScaleFactor { get; }
		bool IsVisible { get; }
		
		double Priority { get; }
		ViewModel ViewModel { get; }
		object ItemState { get; set; }

		void ItemRealized();
		void ItemVirtualized();
	}
}