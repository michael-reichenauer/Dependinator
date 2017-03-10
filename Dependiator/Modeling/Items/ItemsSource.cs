using System.Collections.Generic;
using System.Linq;
using System.Windows;
using Dependiator.Utils;
using Dependiator.Utils.UI.VirtualCanvas;


namespace Dependiator.Modeling.Items
{
	internal class ItemsSource : VirtualItemsSource
	{
		private readonly PriorityQuadTree<IItem> viewItemsTree = new PriorityQuadTree<IItem>();

		private readonly Dictionary<int, IItem> viewItems = new Dictionary<int, IItem>();
		private int currentItemId = 0;

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
				int itemId = currentItemId++;
				virtualItem.ItemState = new ViewItem(itemId, virtualItem.ItemBounds, virtualItem);
				viewItems[itemId] = virtualItem;

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


		public void Add(IItem virtualItem)
		{
			bool isQueryItemsChanged = false;
			Rect currentBounds = TotalBounds;

			int itemId = currentItemId++;
			virtualItem.ItemState = new ViewItem(itemId, virtualItem.ItemBounds, virtualItem);
			viewItems[itemId] = virtualItem;

			viewItemsTree.Insert(virtualItem, virtualItem.ItemBounds, virtualItem.Priority);

			currentBounds.Union(virtualItem.ItemBounds);

			if (virtualItem.ItemBounds.IntersectsWith(lastViewAreaQuery))
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
			ViewItem oldViewItem = (ViewItem)item.ItemState;

			Rect oldItemBounds = oldViewItem.ItemBounds;
			viewItemsTree.Remove(item, oldItemBounds);
			viewItems.Remove(oldViewItem.ItemId);

			int itemId = currentItemId++;
			Rect newItemBounds = item.ItemBounds;
			item.ItemState = new ViewItem(itemId, newItemBounds, item);

			viewItemsTree.Insert(item, newItemBounds, item.Priority);
			viewItems[itemId] = item;

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

			foreach (IItem virtualItem in viewItems.Values)
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
				.Where(i => i.ItemState != null)
				.Select(i => ((ViewItem)i.ItemState).ItemId);
		}


		/// <summary>
		/// Returns the item (commit, branch, merge) corresponding to the specified id.
		/// Commits are in the 0->branchBaseIndex-1 range
		/// Branches are in the branchBaseIndex->mergeBaseIndex-1 range
		/// Merges are mergeBaseIndex-> ... range
		/// </summary>
		protected override object GetItem(int virtualId)
		{
			if (viewItems.TryGetValue(virtualId, out var item))
			{
				return item.ViewModel;
			}

			return null;
		}


		private class ViewItem
		{
			public ViewItem(int itemId, Rect itemBounds, IItem item)
			{
				ItemId = itemId;
				ItemBounds = itemBounds;
				Node = item;
			}

			public int ItemId { get; }

			public Rect ItemBounds { get; set; }

			public IItem Node { get; }
		}
	}
}