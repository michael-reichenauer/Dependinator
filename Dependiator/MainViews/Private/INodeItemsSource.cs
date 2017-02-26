using System;
using System.Collections.Generic;
using System.Windows;
using Dependiator.Utils.UI.VirtualCanvas;


namespace Dependiator.MainViews.Private
{
	internal interface INodeItemsSource
	{
		event EventHandler ExtentChanged;

		VirtualItemsSource VirtualItemsSource { get; }

		Rect TotalBounds { get; }
		bool HasItems { get; }

		void Add(IEnumerable<IItem> virtualItems);

		void Add(IItem virtualItem);

		void Update(IItem item);

		void Remove(IItem item);

		void Clear();


		void TriggerExtentChanged();

		IEnumerable<IItem> GetItemsInArea(Rect nearArea);
		IEnumerable<IItem> GetItemsInView();
		void Remove(IEnumerable<IItem> virtualItems);

		void ItemRealized(int virtualId);
		void ItemVirtualized(int virtualId);
	}
}