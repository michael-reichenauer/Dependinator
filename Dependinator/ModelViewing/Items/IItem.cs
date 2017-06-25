using System.Windows;
using Dependinator.Utils.UI;


namespace Dependinator.ModelViewing.Items
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