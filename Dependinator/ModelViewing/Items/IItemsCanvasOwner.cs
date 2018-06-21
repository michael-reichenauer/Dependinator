using System.Windows;


namespace Dependinator.ModelViewing.Items
{
	internal interface IItemsCanvasOwner
	{
		Rect ItemBounds { get; }

		bool IsShowing { get; }

		bool CanShow { get; }
	}
}