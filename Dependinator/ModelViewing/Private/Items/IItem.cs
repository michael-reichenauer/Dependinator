using System.Windows;

namespace Dependinator.ModelViewing.Private.Items
{
	internal interface IItem
	{
		Rect ItemBounds { get; }
		bool CanShow { get; }
		bool IsShowing { get; }

		object ItemState { get; set; }
		IItemsCanvas ItemOwnerCanvas { get; set; }

		void ItemRealized();
		void ItemVirtualized();
	}
}