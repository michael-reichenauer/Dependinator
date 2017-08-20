using System.Collections.Generic;
using System.Linq;
using System.Windows;
using Dependinator.Utils.UI.VirtualCanvas;

namespace Dependinator.ModelViewing.Private.Items.Private
{
	internal class ItemsSource : VirtualItemsSource
	{
		private readonly IItemsSourceArea itemsSourceArea;
		private readonly PriorityQuadTree<IItem> viewItemsTree = new PriorityQuadTree<IItem>();
		private readonly Dictionary<int, IItem> viewItems = new Dictionary<int, IItem>();
		private readonly Dictionary<int, IItem> removedItems = new Dictionary<int, IItem>();

		//private List<IItem> addingItems;
		//private List<IItem> updatingItems;
		//private List<IItem> removingItems;

		private int currentItemId = 1;
		//private bool isShowing;

		//private bool isItemsChanged;
		//private bool isBoundsChanged;


		//private PriorityQuadTree<IItem> ViewItemsTree =>
		//	viewItemsTree ?? (viewItemsTree = new PriorityQuadTree<IItem>());

		//private Dictionary<int, IItem> ViewItems =>
		//	viewItems ?? (viewItems = new Dictionary<int, IItem>());

		//private Dictionary<int, IItem> RemovedItems =>
		//	removedItems ?? (removedItems = new Dictionary<int, IItem>());

		//private List<IItem> AddingItems => addingItems ?? (addingItems = new List<IItem>());
		//private List<IItem> UpdatingItems => updatingItems ?? (updatingItems = new List<IItem>());
		//private List<IItem> RemovingItems => removingItems ?? (removingItems = new List<IItem>());



		public ItemsSource(IItemsSourceArea itemsSourceArea)
		{
			this.itemsSourceArea = itemsSourceArea;
		}


		public Rect LastViewAreaQuery { get; private set; } = EmptyExtent;




		public Rect TotalBounds { get; private set; } = EmptyExtent;

		protected override Rect VirtualArea => TotalBounds;
		protected override IEnumerable<int> GetVirtualItemIds(Rect viewArea) => GetItemIds(viewArea);
		protected override object GetVirtualItem(int virtualId) => GetItem(virtualId);

		public bool HasItems => viewItems.Any();


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



		public void UpdateAndNotifyAll()
		{
			IReadOnlyList<ItemViewModel> items = viewItemsTree.Cast<ItemViewModel>().ToList();
			Update(items);
			items.ForEach(item => item.NotifyAll());
		}


		public void ItemRealized()
		{
			//isShowing = true;
		}

		public void ItemVirtualized()
		{
			//isShowing = false;
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


		private void Add(IEnumerable<IItem> items)
		{
			bool isQueryItemsChanged = false;
			Rect currentBounds = TotalBounds;

			foreach (IItem item in items)
			{
				int itemId = currentItemId++;
				item.ItemState = new ViewItem(itemId, item.ItemBounds);
				viewItems[itemId] = item;

				viewItemsTree.Insert(item, item.ItemBounds, 0);

				currentBounds.Union(item.ItemBounds);

				if (!isQueryItemsChanged && item.ItemBounds.IntersectsWith(LastViewAreaQuery))
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


		private void Update(IEnumerable<IItem> items)
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


		private void Remove(IEnumerable<IItem> items)
		{
			bool isQueryItemsChanged = false;

			foreach (IItem item in items)
			{
				ViewItem viewItem = (ViewItem)item.ItemState;

				Rect itemBounds = viewItem.ItemBounds;
				viewItemsTree.Remove(item, itemBounds);
				removedItems[viewItem.ItemId] = item;

				item.ItemState = null;

				if (item.CanShow)
				{
					if (itemBounds.IntersectsWith(LastViewAreaQuery))
					{
						isQueryItemsChanged = true;
					}
				}
			}

			ItemsBoundsChanged();

			if (isQueryItemsChanged)
			{
				TriggerItemsChanged();
			}
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
		private IEnumerable<int> GetItemIds(Rect viewArea)
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
		private object GetItem(int virtualId)
		{
			if (viewItems.TryGetValue(virtualId, out var item))
			{
				return item;
			}

			return null;
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