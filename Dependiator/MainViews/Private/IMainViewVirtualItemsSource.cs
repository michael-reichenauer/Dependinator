using System.Collections.Generic;
using System.Windows;
using Dependiator.Utils.UI.VirtualCanvas;


namespace Dependiator.MainViews.Private
{
	internal interface IMainViewItemsSource
	{
		VirtualItemsSource VirtualItemsSource { get; }

		Rect TotalBounds { get; }

		void Add(IEnumerable<IVirtualItem> virtualItems);

		void Update(IVirtualItem virtualItem);

		void TriggerExtentChanged();

		IEnumerable<IVirtualItem> GetItemsInArea(Rect nearArea);
	}
}