using System.Windows;
using Dependinator.ModelViewing.Private.Items.Private;

namespace Dependinator.ModelViewing.Private.Items
{
	internal interface IItem
	{
		Rect ItemBounds { get; }
		bool CanShow { get; }
		bool IsShowing { get; }

		object ItemState { get; set; }
		ItemsCanvas ItemOwnerCanvas { get; set; }

		void ItemRealized();
		void ItemVirtualized();
	}
}