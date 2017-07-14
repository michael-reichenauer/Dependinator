using System.Windows;
using Dependinator.Utils.UI;

namespace Dependinator.ModelViewing.Private.Items
{
	internal interface IItem
	{
		Rect ItemBounds { get; }
		bool CanShow { get; }
		bool IsShowing { get; }


		ViewModel ViewModel { get; }
		object ItemState { get; set; }
		IItemsCanvas ItemsCanvas { get; set; }

		void ItemRealized();
		void ItemVirtualized();
	}
}