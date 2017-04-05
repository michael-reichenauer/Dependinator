using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using Dependiator.Utils;
using Dependiator.Utils.UI.VirtualCanvas;


namespace Dependiator.Modeling.Items
{
	internal class ItemsSource : VirtualItemsSource
	{
		private readonly ItemsCanvas itemsCanvas;
		private readonly PriorityQuadTree<IItem> viewItemsTree = new PriorityQuadTree<IItem>();

		private readonly Dictionary<int, IItem> viewItems = new Dictionary<int, IItem>();
		private readonly Dictionary<int, IItem> removedItems = new Dictionary<int, IItem>();
		public static int ItemCount = 0;

		private int currentItemId = 0;

		private static int currentId = 0;
		private int id = 0;

		public Rect LastViewAreaQuery { get; private set; } = EmptyExtent;

	//	LastViewAreaQuery { get; set; }

	//private Rect lastViewAreaQuery = EmptyExtent;


		public ItemsSource(ItemsCanvas itemsCanvas)
		{
			this.itemsCanvas = itemsCanvas;
			id = ++currentId;
			Log.Debug($"Id: {id}");
		}

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

				viewItemsTree.Insert(virtualItem, virtualItem.ItemBounds, 0);
				ItemCount++;

				if (virtualItem.CanShow)
				{
					currentBounds.Union(virtualItem.ItemBounds);

					if (!isQueryItemsChanged && virtualItem.ItemBounds.IntersectsWith(LastViewAreaQuery))
					{
						isQueryItemsChanged = true;
					}
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

			viewItemsTree.Insert(virtualItem, virtualItem.ItemBounds, 0);
			ItemCount++;

			if (virtualItem.CanShow)
			{
				currentBounds.Union(virtualItem.ItemBounds);

				if (virtualItem.ItemBounds.IntersectsWith(LastViewAreaQuery))
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

			Rect oldItemBounds = oldViewItem.ItemBounds;
			viewItemsTree.Remove(item, oldItemBounds);

			Rect newItemBounds = item.ItemBounds;
			item.ItemState = new ViewItem(oldViewItem.ItemId, newItemBounds, item);

			viewItemsTree.Insert(item, newItemBounds, 0);

			ItemsBoundsChanged();

			if (oldItemBounds.IntersectsWith(LastViewAreaQuery)
				|| newItemBounds.IntersectsWith(LastViewAreaQuery))
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
				removedItems[viewItem.ItemId] = item;

				ItemCount--;
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

			ItemCount--;
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
			return GetItemsInArea(LastViewAreaQuery);
		}


		public void ItemsBoundsChanged()
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
			// !!!!###### Enable inflate in Zoomable line 1369

			//Log.Debug($"Id: {id}, ViewPort {itemsCanvas.CurrentViewPort}, Inflated: {viewArea}, ");

			if (itemsCanvas.ParentItemsCanvas != null 
				&& itemsCanvas.ParentItemsCanvas.LastViewAreaQuery != Rect.Empty)
			{
				Rect parentViewArea = itemsCanvas.ParentItemsCanvas.LastViewAreaQuery;

				Rect itemRect = itemsCanvas.ItemBounds;

				Double scaleFactor = itemsCanvas.ScaleFactor;

				//Double scaleFactor = itemsCanvas.ParentItemsCanvas.ZoomableCanvas.Scale 
				//	/ itemsCanvas.ZoomableCanvas.Scale;

				double x = (parentViewArea.X - itemRect.X) * scaleFactor;
				double y = (parentViewArea.Y - itemRect.Y) * scaleFactor;
				double w = parentViewArea.Width * scaleFactor;
				double h = parentViewArea.Height * scaleFactor;

				Rect scaledParentViewArea = new Rect(x + viewArea.X, y + viewArea.Y, w, h);		

				viewArea.Intersect(scaledParentViewArea);
			}


			LastViewAreaQuery = viewArea;

			if (viewArea == Rect.Empty)
			{
				return Enumerable.Empty<int>();
			}

			IEnumerable<int> itemIds = viewItemsTree.GetItemsIntersecting(viewArea)
				.Where(i => i.ItemState != null && i.CanShow)
				.Select(i => ((ViewItem)i.ItemState).ItemId);

			//Log.Debug($"Id: {id}, Count: {itemIds.Count()}");
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

			public Rect ItemBounds { get;}

			public IItem Node { get; }
		}
	}
}