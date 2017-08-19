using System.Collections.Generic;
using System.Linq;
using System.Windows;
using Dependinator.Utils;
using Dependinator.Utils.UI.VirtualCanvas;

namespace Dependinator.ModelViewing.Private.Items.Private
{
	internal class ItemsSource : VirtualItemsSource
	{
		private readonly IItemsSourceArea itemsSourceArea;
		private readonly PriorityQuadTree<IItem> viewItemsTree = new PriorityQuadTree<IItem>();

		private readonly Dictionary<int, IItem> viewItems = new Dictionary<int, IItem>();
		private readonly Dictionary<int, IItem> removedItems = new Dictionary<int, IItem>();


		private int currentItemId = 0;


		public Rect LastViewAreaQuery { get; private set; } = EmptyExtent;


		public ItemsSource(IItemsSourceArea itemsSourceArea)
		{
			this.itemsSourceArea = itemsSourceArea;
		}

		public Rect TotalBounds { get; private set; } = EmptyExtent;

		protected override Rect VirtualArea => TotalBounds;


		public bool HasItems => viewItems.Any();


		public void Add(IItem item)
		{
			bool isQueryItemsChanged = false;
			Rect currentBounds = TotalBounds;

			int itemId = currentItemId++;
			item.ItemState = new ViewItem(itemId, item.ItemBounds, item);
			viewItems[itemId] = item;

			viewItemsTree.Insert(item, item.ItemBounds, 0);


			if (item.CanShow)
			{
				currentBounds.Union(item.ItemBounds);

				if (item.ItemBounds.IntersectsWith(LastViewAreaQuery))
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


		public void Update(IItem item)
		{
			ViewItem oldViewItem = (ViewItem)item.ItemState;
			if (oldViewItem == null)
			{
				return;
			}

			Rect oldItemBounds = oldViewItem.ItemBounds;
			viewItemsTree.Remove(item, oldItemBounds);

			Rect newItemBounds = item.ItemBounds;
			item.ItemState = new ViewItem(oldViewItem.ItemId, newItemBounds, item);

			viewItemsTree.Insert(item, newItemBounds, 0);
			//Log.Debug($"Updated {id} count:{viewItemsTree.Count()}");

			ItemsBoundsChanged();

			if (oldItemBounds.IntersectsWith(LastViewAreaQuery)
				|| newItemBounds.IntersectsWith(LastViewAreaQuery))
			{
				TriggerItemsChanged();
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
				item.ItemState = new ViewItem(oldViewItem.ItemId, newItemBounds, item);

				viewItemsTree.Insert(item, newItemBounds, 0);
				//Log.Debug($"Updated {id} count:{viewItemsTree.Count()}");

				if (oldItemBounds.IntersectsWith(LastViewAreaQuery)
						|| newItemBounds.IntersectsWith(LastViewAreaQuery))
				{
					isTriggerItemsChanged = true;
				}
			}

			ItemsBoundsChanged();

			if (isTriggerItemsChanged)
			{
				TriggerItemsChanged();
			}
		}

		public IReadOnlyList<T> GetAll<T>()
		{
			return viewItemsTree.Cast<T>().ToList();
		}


		public void Remove(IItem item)
		{
			ViewItem viewItem = (ViewItem)item.ItemState;

			if (viewItem == null)
			{
				return;
			}

			Rect itemBounds = viewItem.ItemBounds;
			if (viewItemsTree.Remove(item, itemBounds))
			{
				removedItems[viewItem.ItemId] = item;
			}
			else
			{
				Log.Warn($"Failed to remove {item}");
			}


			item.ItemState = null;

			ItemsBoundsChanged();

			if (item.CanShow)
			{
				if (itemBounds.IntersectsWith(LastViewAreaQuery))
				{
					TriggerItemsChanged();
				}
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
				removedItems.Remove(virtualId);
				viewItems.Remove(virtualId);
			}
		}


		public void RemoveAll()
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
			return GetItemsInArea(LastViewAreaQuery);
		}


		private void ItemsBoundsChanged()
		{
			Rect currentBounds = EmptyExtent;

			foreach (IItem virtualItem in viewItems.Values)
			{
				if (virtualItem.CanShow)
				{
					currentBounds.Union(virtualItem.ItemBounds);
				}
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
			//Inflate is Enabled in Zoomable line 1369
			if (viewArea == Rect.Empty)
			{
				return Enumerable.Empty<int>();
			}

			if (!itemsSourceArea.IsRoot)
			{
				// Adjust view area to compensate for ancestors
				Rect ancestorsViewArea = itemsSourceArea.GetHierarchicalVisualArea();
				viewArea.Intersect(ancestorsViewArea);

				if (viewArea == Rect.Empty)
				{
					return Enumerable.Empty<int>();
				}
			}

			// For smother panning, include an area that is a little a little larger than current view
			viewArea.Inflate(viewArea.Width / 10, viewArea.Height / 10);

			LastViewAreaQuery = viewArea;

			IEnumerable<int> itemIds = viewItemsTree.GetItemsIntersecting(viewArea)
				.Where(i => i.ItemState != null && i.CanShow)
				.Select(i => ((ViewItem)i.ItemState).ItemId);

			return itemIds;
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
				return item;
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

			public Rect ItemBounds { get; }

			public IItem Node { get; }
		}
	}
}