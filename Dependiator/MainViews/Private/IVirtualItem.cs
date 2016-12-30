using System.Windows;


namespace Dependiator.MainViews.Private
{
	internal interface IVirtualItem
	{
		object VirtualId { get; set; }

		object ViewModel { get; }

		Rect ItemBounds { get; }
	}
}