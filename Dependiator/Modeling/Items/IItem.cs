using System.Windows;
using Dependiator.Utils.UI;


namespace Dependiator.Modeling.Items
{
	internal interface IItem
	{
		Rect ItemBounds { get; }
		bool CanShow { get; }
		bool IsShowing { get; }

		ViewModel ViewModel { get; }
		object ItemState { get; set; }

		void ItemRealized();
		void ItemVirtualized();
	}
}