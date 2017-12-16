using System.Windows;


namespace Dependinator.ModelViewing.ModelHandling.Private.Items
{
	internal interface IItemsCanvasBounds
	{
		Rect ItemBounds { get; }

		bool IsShowing { get; }

		bool CanShow { get; }
	}
}