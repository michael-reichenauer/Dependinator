using System.Windows;

namespace Dependinator.ModelViewing.Private.Items
{
	internal interface IItemsCanvasBounds
	{
		Rect ItemBounds { get; }

		bool IsShowing { get; }
	}
}