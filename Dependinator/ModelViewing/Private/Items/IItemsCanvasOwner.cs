using System.Windows;


namespace Dependinator.ModelViewing.Private.Items
{
	internal interface IItemsCanvasOwner
	{
		Rect ItemBounds { get; }

		bool IsShowing { get; }

		bool CanShow { get; }
	}
}