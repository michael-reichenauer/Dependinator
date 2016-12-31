using System.Collections.Generic;
using System.Windows;
using Dependiator.Utils.UI.VirtualCanvas;


namespace Dependiator.MainViews.Private
{
	internal interface IMainViewItemsSource
	{
		VirtualItemsSource VirtualItemsSource { get; }

		Rect TotalBounds { get; }

		void Add(IEnumerable<IItem> virtualItems);

		void Update(IItem item);

		void TriggerExtentChanged();

		IEnumerable<IItem> GetItemsInArea(Rect nearArea);
		IEnumerable<IItem> GetItemsInView();
	}
}