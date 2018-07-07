using System.Collections.Generic;
using System.Linq;
using System.Windows;
using Dependinator.Utils.UI;
using Dependinator.Utils.UI.VirtualCanvas;


namespace Dependinator.ModelViewing.Items.Private
{
	internal class ItemsSource : VirtualItemsSource
	{
		private static readonly int ItemsChangedInterval = 300;
		private static readonly int ExtentChangedInterval = 300;

		private readonly VisualAreaHandler visualAreaHandler;
		private readonly PriorityQuadTree<IItem> viewItemsTree = new PriorityQuadTree<IItem>();
		private readonly Dictionary<int, IItem> viewItems = new Dictionary<int, IItem>();
		private readonly Dictionary<int, IItem> removedItems = new Dictionary<int, IItem>();
		private readonly ThrottleDispatcher extentChangeThrottle = new ThrottleDispatcher();
		private readonly ThrottleDispatcher itemsChangedThrottle = new ThrottleDispatcher();

		private int currentItemId = 1;
		private Rect lastViewAreaQuery = EmptyExtent;
		private Rect totalBounds = EmptyExtent;


		public ItemsSource(VisualAreaHandler visualAreaHandler)
		{
			this.visualAreaHandler = visualAreaHandler;
		}

		// VirtualItemsSource overrides
		protected override Rect VirtualArea => totalBounds;
		protected override IEnumerable<int> GetVirtualItemIds(Rect viewArea) => GetItemIds(viewArea);
		protected override object GetVirtualItem(int virtualId) => GetItem(virtualId);


		public IEnumerable<IItem> GetAllItems() => viewItemsTree;


		public void Add(IItem item)
		{
			Add(new[] { item });
		}


		public void Update(IItem item)
		{
			Update(new[] { item });
		}


		public void Remove(IItem item)
		{
			Remove(new[] { item });
		}



		public void RemoveAll()
		{
			List<IItem> allItems = viewItemsTree.ToList();
			Remove(allItems);
		}


		private void Add(IEnumerable<IItem> items)
		{
			bool isQueryItemsChanged = false;
			Rect currentBounds = totalBounds;

			foreach (IItem item in items)
			{
				if (viewItemsTree.Any())
				{
					currentBounds.Union(item.ItemBounds);
				}
				else
				{
					currentBounds = item.ItemBounds;
				}

				int itemId = currentItemId++;
				item.ItemState = new ViewItem(itemId, item.ItemBounds);
				viewItems[itemId] = item;

				viewItemsTree.Insert(item, item.ItemBounds, item.Priority);


				if (!isQueryItemsChanged && item.ItemBounds.IntersectsWith(lastViewAreaQuery))
				{
					isQueryItemsChanged = true;
				}
			}

			if (currentBounds != totalBounds)
			{
				totalBounds = currentBounds;
				SendExtentChanged();
			}

			if (isQueryItemsChanged)
			{
				SendItemsChanged();
			}
		}


		public void Update(IEnumerable<IItem> items)
		{
			bool isTriggerItemsChanged = false;

			foreach (IItem item in items)
			{
				ViewItem oldViewItem = (ViewItem)item.ItemState;
				if (oldViewItem == null)
				{
					return;
				}

				Rect oldItemBounds = oldViewItem.ItemBounds;
				viewItemsTree.Remove(item, oldItemBounds);

				Rect newItemBounds = item.ItemBounds;
				item.ItemState = new ViewItem(oldViewItem.ItemId, newItemBounds);

				viewItemsTree.Insert(item, newItemBounds, item.Priority);

				if (oldItemBounds.IntersectsWith(lastViewAreaQuery)
						|| newItemBounds.IntersectsWith(lastViewAreaQuery))
				{
					isTriggerItemsChanged = true;
				}
			}

			ItemsBoundsChanged();

			if (isTriggerItemsChanged)
			{
				SendItemsChanged();
			}
		}


		private void Remove(IEnumerable<IItem> items)
		{
			bool isQueryItemsChanged = false;

			foreach (IItem item in items.Where(i => i != null && i.ItemState != null))
			{
				ViewItem viewItem = (ViewItem)item.ItemState;

				Rect itemBounds = viewItem.ItemBounds;
				viewItemsTree.Remove(item, itemBounds);
				removedItems[viewItem.ItemId] = item;
				// Log.Debug($"Remove item {viewItem.ItemId}");

				item.ItemState = null;

				if (item.CanShow)
				{
					if (itemBounds.IntersectsWith(lastViewAreaQuery))
					{
						isQueryItemsChanged = true;
					}
				}
			}

			ItemsBoundsChanged();

			if (isQueryItemsChanged)
			{
				// Log.Debug("TriggerItemsChanged");
				SendItemsChanged();
			}
		}


		public void ItemRealized(int virtualId)
		{
			if (viewItems.TryGetValue(virtualId, out var item))
			{
				item.ItemRealized();
			}
		}


		public void ItemVirtualized(int virtualId)
		{
			if (viewItems.TryGetValue(virtualId, out IItem item))
			{
				item.ItemVirtualized();
			}

			if (removedItems.ContainsKey(virtualId))
			{
				// Since remove does not immediately remove items, they are removed when virtualized
				removedItems.Remove(virtualId);
				viewItems.Remove(virtualId);
			}
		}



		private void ItemsBoundsChanged()
		{
			Rect currentBounds = EmptyExtent;
			bool isFirst = true;

			foreach (IItem virtualItem in viewItemsTree)
			{
				if (virtualItem.CanShow)
				{
					if (isFirst)
					{
						currentBounds = virtualItem.ItemBounds;
						isFirst = false;
					}
					else
					{
						currentBounds.Union(virtualItem.ItemBounds);
					}
				}
			}

			if (currentBounds != totalBounds)
			{
				totalBounds = currentBounds;
				SendExtentChanged();
			}
		}


		/// <summary>
		/// Returns range of item ids, which are visible in the area currently shown
		/// </summary>
		private IEnumerable<int> GetItemIds(Rect viewArea)
		{
			viewArea = visualAreaHandler.GetVisualArea(viewArea);

			if (viewArea == Rect.Empty)
			{
				return Enumerable.Empty<int>();
			}

			// For smother panning, include an area that is a little a little larger than current view
			// Inflate was Enabled in ZoomableCanvas line 1378
			viewArea.Inflate(viewArea.Width / 10, viewArea.Height / 10);

			lastViewAreaQuery = viewArea;

			IEnumerable<int> itemIds = viewItemsTree.GetItemsIntersecting(viewArea)
				.Where(i => i.ItemState != null && i.CanShow)
				.Select(i => ((ViewItem)i.ItemState).ItemId);

			// Log.Debug($"Items in {itemsSourceArea}: {string.Join(", ", itemIds)}");

			return itemIds;
		}


		/// <summary>
		/// Returns the item (commit, branch, merge) corresponding to the specified id.
		/// Commits are in the 0->branchBaseIndex-1 range
		/// Branches are in the branchBaseIndex->mergeBaseIndex-1 range
		/// Merges are mergeBaseIndex-> ... range
		/// </summary>
		private object GetItem(int virtualId)
		{
			if (viewItems.TryGetValue(virtualId, out var item))
			{
				return item;
			}

			return null;
		}


		private void SendItemsChanged()
		{
			itemsChangedThrottle.Throttle(ItemsChangedInterval, _ => TriggerItemsChanged());
		}


		private void SendExtentChanged()
		{
			extentChangeThrottle.Throttle(ExtentChangedInterval, _ => TriggerExtentChanged());
		}



		private class ViewItem
		{
			public ViewItem(int itemId, Rect itemBounds)
			{
				ItemId = itemId;
				ItemBounds = itemBounds;
			}

			public int ItemId { get; }

			public Rect ItemBounds { get; }
		}
	}
}