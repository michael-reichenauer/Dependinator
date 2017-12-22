using System.Windows;


namespace Dependinator.ModelViewing.Items
{
	internal interface IItemsCanvasBounds
	{
		Rect ItemBounds { get; }

		bool IsShowing { get; }

		bool CanShow { get; }
	}
}