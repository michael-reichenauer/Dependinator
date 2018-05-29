using System.Windows;


namespace Dependinator.ModelViewing.Items
{
	internal interface IItem
	{
		Rect ItemBounds { get; }
		double Priority { get; }
		bool CanShow { get; }
		bool IsShowing { get; }

		object ItemState { get; set; }
		ItemsCanvas ItemOwnerCanvas { get; set; }

		void ItemRealized();
		void ItemVirtualized();
		void MoveItem(Vector moveOffset);
	}
}