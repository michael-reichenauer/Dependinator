using System.Windows;
using Dependiator.Utils.UI;


namespace Dependiator.MainViews.Private
{
	internal interface IItem
	{
		object VirtualId { get; set; }

		ViewModel ViewModel { get; }

		Rect ItemBounds { get; }

		double Priority { get; }

		int ZIndex { get; set; }

		void ZoomChanged();
	}
}