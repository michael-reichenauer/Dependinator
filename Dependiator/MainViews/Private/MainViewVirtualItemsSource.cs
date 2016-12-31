using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using Dependiator.Utils;
using Dependiator.Utils.UI.VirtualCanvas;


namespace Dependiator.MainViews.Private
{
	[SingleInstance]
	internal class MainViewItemsSource : VirtualItemsSource, IMainViewItemsSource
	{
		private readonly PriorityQuadTree<IVirtualItem> viewItemsTree = new PriorityQuadTree<IVirtualItem>();
		private readonly List<IVirtualItem> viewItems = new List<IVirtualItem>();

		private Rect lastViewAreaQuery = EmptyExtent;

		public Rect TotalBounds { get; private set; } = EmptyExtent;

		protected override Rect VirtualArea => TotalBounds;


		public VirtualItemsSource VirtualItemsSource => this;


		public void Add(IEnumerable<IVirtualItem> virtualItems)
		{
			bool isQueryItemsChanged = false;
			Rect currentBounds = TotalBounds;

			foreach (IVirtualItem virtualItem in virtualItems)
			{
				virtualItem.VirtualId = new ViewItem(viewItems.Count, virtualItem.ItemBounds);
				viewItems.Add(virtualItem);

				viewItemsTree.Insert(virtualItem, virtualItem.ItemBounds, virtualItem.Priority);

				currentBounds.Union(virtualItem.ItemBounds);

				if (!isQueryItemsChanged && virtualItem.ItemBounds.IntersectsWith(lastViewAreaQuery))
				{
					isQueryItemsChanged = true;
				}
			}

			if (currentBounds != TotalBounds)
			{
				TotalBounds = currentBounds;
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

			ItemsBoundsChanged();

			if (oldItemBounds.IntersectsWith(lastViewAreaQuery) 
				|| newItemBounds.IntersectsWith(lastViewAreaQuery))
			{
				TriggerItemsChanged();
			}
		}


		public void ItemsBoundsChanged()
		{
			Rect currentBounds = EmptyExtent;

			foreach (IVirtualItem virtualItem in viewItems)
			{
				currentBounds.Union(virtualItem.ItemBounds);
			}

			if (currentBounds != TotalBounds)
			{
				TotalBounds = currentBounds;
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
			if (virtualId >= viewItems.Count)
			{
				return null;
			}

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