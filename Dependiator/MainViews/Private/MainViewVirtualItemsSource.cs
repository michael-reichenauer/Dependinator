using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using Dependiator.Utils;
using Dependiator.Utils.UI.VirtualCanvas;


namespace Dependiator.MainViews.Private
{
	internal interface IMainViewVirtualItemsSource
	{
		VirtualItemsSource VirtualItemsSource { get; }

		Rect Extent { get; }

		void Add(IEnumerable<IVirtualItem> virtualItems);
		void Update(IVirtualItem virtualItem);

		void ItemsAreaChanged();
		
		void TriggerInvalidated();
		void TriggerExtentChanged();
	}


	[SingleInstance]
	internal class MainViewVirtualItemsSource : VirtualItemsSource, IMainViewVirtualItemsSource
	{
		private readonly PriorityQuadTree<IVirtualItem> viewItemsTree = new PriorityQuadTree<IVirtualItem>();
		private readonly List<IVirtualItem> viewItems = new List<IVirtualItem>();

		private Rect virtualArea = EmptyExtent;
		private Rect lastViewAreaQuery = EmptyExtent;

		protected override Rect VirtualArea => virtualArea;


		public VirtualItemsSource VirtualItemsSource => this;


		public void Add(IEnumerable<IVirtualItem> virtualItems)
		{
			bool isQueryItemsChanged = false;
			Rect newArea = virtualArea;

			foreach (IVirtualItem virtualItem in virtualItems)
			{
				virtualItem.VirtualId = new ViewItem(viewItems.Count, virtualItem.ItemBounds);
				viewItems.Add(virtualItem);

				viewItemsTree.Insert(virtualItem, virtualItem.ItemBounds, virtualItem.Priority);

				newArea.Union(virtualItem.ItemBounds);

				if (!isQueryItemsChanged && virtualItem.ItemBounds.IntersectsWith(lastViewAreaQuery))
				{
					isQueryItemsChanged = true;
				}
			}

			if (newArea != virtualArea)
			{
				virtualArea = newArea;
				TriggerExtentChanged();
			}

			if (isQueryItemsChanged)
			{
				TriggerItemsChanged();
			}		
		}


		//public void Add(IVirtualItem virtualItem)
		//{
		//	Rect newArea = virtualArea;

		//	ViewItem viewItem = new ViewItem(viewItems.Count, virtualItem);
		//	viewItems.Add(viewItem);

		//	viewItemsTree.Insert(viewItem, viewItem.ItemBounds, 0);

		//	newArea.Union(viewItem.ItemBounds);

		//	if (newArea != virtualArea)
		//	{
		//		virtualArea = newArea;
		//		TriggerExtentChanged();
		//	}

		//	if (viewItem.ItemBounds.IntersectsWith(lastViewAreaQuery))
		//	{
		//		TriggerItemsChanged();
		//	}
		//}


		public void Update(IVirtualItem virtualItem)
		{
			ViewItem viewItem = (ViewItem)virtualItem.VirtualId;

			Rect oldItemBounds = viewItem.ItemBounds;
			viewItemsTree.Remove(virtualItem, oldItemBounds);

			Rect newItemBounds = virtualItem.ItemBounds;
			viewItem.ItemBounds = newItemBounds;
			viewItemsTree.Insert(virtualItem, viewItem.ItemBounds, 0);

			ItemsAreaChanged();

			if (oldItemBounds.IntersectsWith(lastViewAreaQuery) 
				|| newItemBounds.IntersectsWith(lastViewAreaQuery))
			{
				TriggerItemsChanged();
			}
		}


		public void ItemsAreaChanged()
		{
			Rect previousArea = virtualArea;
			virtualArea = EmptyExtent;

			foreach (IVirtualItem virtualItem in viewItems)
			{
				virtualArea.Union(virtualItem.ItemBounds);
			}

			if (previousArea != virtualArea)
			{
				TriggerExtentChanged();
			}
		}


		/// <summary>
		/// Returns range of item ids, which are visible in the area currently shown
		/// </summary>
		protected override IEnumerable<int> GetItemIds(Rect viewArea)
		{
			lastViewAreaQuery = viewArea;

			return viewItemsTree.GetItemsIntersecting(viewArea)
				.Select(i => ((ViewItem)i.VirtualId).Index);
		}


		/// <summary>
		/// Returns the item (commit, branch, merge) corresponding to the specified id.
		/// Commits are in the 0->branchBaseIndex-1 range
		/// Branches are in the branchBaseIndex->mergeBaseIndex-1 range
		/// Merges are mergeBaseIndex-> ... range
		/// </summary>
		protected override object GetItem(int virtualId)
		{
			return viewItems[virtualId].ViewModel;
		}


		private class ViewItem
		{
			public ViewItem(int index, Rect itemBounds)
			{
				Index = index;
				ItemBounds = itemBounds;
			}

			public int Index { get; set; }

			public Rect ItemBounds { get; set; }
		}
	}
}