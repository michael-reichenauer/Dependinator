using System.Windows;


namespace Dependiator.MainViews.Private
{
	internal interface IItem
	{
		object VirtualId { get; set; }

		object ViewModel { get; }

		Rect ItemBounds { get; }

		double Priority { get; }

		int ZIndex { get; set; }

		void ZoomChanged();
	}
}