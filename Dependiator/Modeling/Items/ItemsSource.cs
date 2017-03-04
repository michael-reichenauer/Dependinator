using System.Collections.Generic;
using System.Linq;
using System.Windows;
using Dependiator.Utils.UI.VirtualCanvas;


namespace Dependiator.Modeling.Items
{
	internal class ItemsSource : VirtualItemsSource
	{
		private readonly PriorityQuadTree<IItem> viewItemsTree = new PriorityQuadTree<IItem>();
		private readonly List<IItem> viewItems = new List<IItem>();

		private Rect lastViewAreaQuery = EmptyExtent;

		public Rect TotalBounds { get; private set; } = EmptyExtent;

		protected override Rect VirtualArea => TotalBounds;


		public VirtualItemsSource VirtualItemsSource => this;

		public bool HasItems => viewItems.Any();

		public void Add(IEnumerable<IItem> virtualItems)
		{
			bool isQueryItemsChanged = false;
			Rect currentBounds = TotalBounds;

			foreach (IItem virtualItem in virtualItems)
			{
				virtualItem.ItemState = new ViewItem(viewItems.Count, virtualItem.ItemCanvasBounds, virtualItem);
				viewItems.Add(virtualItem);

				viewItemsTree.Insert(virtualItem, virtualItem.ItemCanvasBounds, virtualItem.Priority);

				currentBounds.Union(virtualItem.ItemCanvasBounds);

				if (!isQueryItemsChanged && virtualItem.ItemCanvasBounds.IntersectsWith(lastViewAreaQuery))
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


		public void Add(IItem virtualItem)
		{
			bool isQueryItemsChanged = false;
			Rect currentBounds = TotalBounds;

			virtualItem.ItemState = new ViewItem(viewItems.Count, virtualItem.ItemCanvasBounds, virtualItem);
			viewItems.Add(virtualItem);

			viewItemsTree.Insert(virtualItem, virtualItem.ItemCanvasBounds, virtualItem.Priority);

			currentBounds.Union(virtualItem.ItemCanvasBounds);

			if (virtualItem.ItemCanvasBounds.IntersectsWith(lastViewAreaQuery))
			{
				isQueryItemsChanged = true;
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


		public void Update(IItem item)
		{
			ViewItem viewItem = (ViewItem)item.ItemState;

			Rect oldItemBounds = viewItem.ItemBounds;
			viewItemsTree.Remove(item, oldItemBounds);

			Rect newItemBounds = item.ItemCanvasBounds;
			viewItem.ItemBounds = newItemBounds;
			viewItemsTree.Insert(item, viewItem.ItemBounds, item.Priority);

			ItemsBoundsChanged();

			if (oldItemBounds.IntersectsWith(lastViewAreaQuery)
				|| newItemBounds.IntersectsWith(lastViewAreaQuery))
			{
				TriggerItemsChanged();
			}
		}


		public void Remove(IEnumerable<IItem> virtualItems)
		{
			bool isQueryItemsChanged = false;
			foreach (IItem item in virtualItems)
			{
				ViewItem viewItem = (ViewItem)item.ItemState;

				Rect itemBounds = viewItem.ItemBounds;
				viewItemsTree.Remove(item, itemBounds);
				item.ItemState = null;

				if (itemBounds.IntersectsWith(lastViewAreaQuery))
				{
					isQueryItemsChanged = true;
				}
			}

			ItemsBoundsChanged();

			if (isQueryItemsChanged)
			{
				TriggerItemsChanged();
			}
		}


		public void ItemRealized(int virtualId)
		{
			if (virtualId >= viewItems.Count)
			{
				return;
			}

			viewItems[virtualId].ItemRealized();
		}


		public void ItemVirtualized(int virtualId)
		{
			if (virtualId >= viewItems.Count)
			{
				return;
			}

			viewItems[virtualId].ItemVirtualized();
		}


		public void Remove(IItem item)
		{
			ViewItem viewItem = (ViewItem)item.ItemState;

			Rect itemBounds = viewItem.ItemBounds;
			viewItemsTree.Remove(item, itemBounds);
			item.ItemState = null;

			ItemsBoundsChanged();

			if (itemBounds.IntersectsWith(lastViewAreaQuery))
			{
				TriggerItemsChanged();
			}
		}


		public void Clear()
		{
			viewItemsTree.Clear();
			TriggerInvalidated();
		}


		public IEnumerable<IItem> GetItemsInArea(Rect area)
		{
			return viewItemsTree.GetItemsIntersecting(area).Select(i => i);
		}


		public IEnumerable<IItem> GetItemsInView()
		{
			return GetItemsInArea(lastViewAreaQuery);
		}


		public void ItemsBoundsChanged()
		{
			Rect currentBounds = EmptyExtent;

			foreach (IItem virtualItem in viewItems)
			{
				currentBounds.Union(virtualItem.ItemCanvasBounds);
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
				.Select(i => ((ViewItem)i.ItemState).Index);
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
			public ViewItem(int index, Rect itemBounds, IItem item)
			{
				Index = index;
				ItemBounds = itemBounds;
				Node = item;
			}

			public int Index { get; set; }

			public Rect ItemBounds { get; set; }

			public IItem Node { get; }
		}
	}
}