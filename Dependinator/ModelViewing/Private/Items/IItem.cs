using System.Windows;


namespace Dependinator.ModelViewing.Private.Items
{
	internal interface IItem
	{
		Rect ItemBounds { get; }
		double Priority { get; }
		bool CanShow { get; }
		bool IsShowing { get; }
		ItemsCanvas ItemOwnerCanvas { get; set; }

		object ItemState { get; set; }

		void ItemRealized();
		void ItemVirtualized();
		void MoveItem(Vector moveOffset);
	}
}