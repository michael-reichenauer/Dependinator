//using System;
//using System.Collections.Generic;
//using System.Linq;
//using System.Windows;
//using Dependiator.MainViews.Private;
//using Dependiator.Utils.UI;


//namespace Dependiator.Modeling
//{
//	internal abstract class Item : IItem
//	{
//		private static readonly double ZoomSpeed = 2000.0;

//		private Item parentItem;
//		private readonly List<Item> childItems = new List<Item>();
//		private Rect itemBounds;

//		protected readonly IItemService itemService;

//		private int xf = 1;
//		private int yf = 1;
//		private int wf = 0;
//		private int hf = 0;

//		protected Item(IItemService itemService, Item parentItem)
//		{
//			this.itemService = itemService;
//			ParentItem = parentItem;
//		}

//		public Rect ItemCanvasBounds { get; protected set; }
//		public double ZIndex { get; set; }
//		public double Priority { get; protected set; }
//		public abstract ViewModel ViewModel { get; }
//		public object ItemState { get; set; }

//		public bool IsAdded => ItemState != null || ParentItem == null;
//		public bool IsRealized { get; private set; }


//		public void NotifyAll()
//		{
//			if (IsAdded)
//			{
//				ViewModel.NotifyAll();
//			}
//		}

//		public virtual void ItemRealized() => IsRealized = true;
		
//		public virtual void ItemVirtualized() => IsRealized = false;
		


//		public int ItemLevel => ParentItem?.ItemLevel + 1 ?? 0;

//		public double ThisItemScaleFactor { get; set; } = 7;

//		public double CanvasScale => itemService.CanvasScale;

//		public double ItemScale => CanvasScale * ItemScaleFactor;

//		public double ItemScaleFactor
//		{
//			get
//			{
//				if (ParentItem == null)
//				{
//					return ThisItemScaleFactor;
//				}

//				return ParentItem.ItemScaleFactor / ThisItemScaleFactor;
//			}
//		}

//		public IReadOnlyList<Item> ChildItems => childItems;

//		public abstract bool CanBeShown();

//		public Item ParentItem
//		{
//			get { return parentItem; }
//			set
//			{
//				parentItem = value;

//				if (parentItem != null)
//				{
//					parentItem.AddChildItem(this);
//				}
//			}
//		}


//		public Rect ItemBounds
//		{
//			get { return itemBounds; }
//			set
//			{
//				itemBounds = value;
//				SetItemCanvasBounds();

//				ItemBoundsChanged();
//			}
//		}


//		private void SetItemCanvasBounds()
//		{
//			ItemCanvasBounds = ParentItem?.GetChildItemCanvasBounds(itemBounds) ?? itemBounds;
//		}


//		public Size ItemViewSize =>
//			new Size(ItemCanvasBounds.Width * CanvasScale, ItemCanvasBounds.Height * CanvasScale);


//		public void ShowItem()
//		{
//			if (!IsAdded && CanBeShown())
//			{
//				itemService.ShowItem(this);
//			}
//		}


//		public void ShowChildren()
//		{
//			ChildItems.ForEach(item => item.ShowItem());
//		}


//		public void HideNode()
//		{
//			itemService.HideItems(GetHidableDecedentAndSelf());
//		}


//		public void HideChildren()
//		{
//			itemService.HideItems(GetHidableDecedent());
//		}


//		public void MoveOrResize(Point canvasPoint, Vector viewOffset, bool isFirst)
//		{
//			if (isFirst)
//			{
//				GetMoveResizeFactors(canvasPoint);
//			}

//			Vector itemOffset = new Vector(viewOffset.X / ItemScale, viewOffset.Y / ItemScale);

//			Point newItemLocation = new Point(
//				ItemBounds.X + xf * itemOffset.X, 
//				ItemBounds.Y + yf * itemOffset.Y);

//			double width = ItemBounds.Size.Width + (wf * itemOffset.X);
//			double height = this.itemBounds.Size.Height + (hf * itemOffset.Y);

//			if (width < 0 || height < 0)
//			{
//				return;
//			}

//			Size newItemSize = new Size(width, height);
//			Rect newItemBounds = new Rect(newItemLocation, newItemSize);

//			if ((newItemBounds.X + newItemBounds.Width > ParentItem.ItemBounds.Width * ThisItemScaleFactor)
//			    || (newItemBounds.Y + newItemBounds.Height > ParentItem.ItemBounds.Height * ThisItemScaleFactor)
//			    || newItemBounds.X < 0
//			    || newItemBounds.Y < 0)
//			{
//				return;
//			}

//			ItemBounds = newItemBounds;
//			itemService.UpdateItem(this);
//			NotifyAll();


//			foreach (Item childItem in ChildItems)
//			{
//				if (wf != 0 && hf != 0)
//				{
//					// Resizing iten, ensure that child items do not move
//					childItem.ItemBounds = new Rect(
//						new Point(
//							childItem.ItemBounds.X - xf * itemOffset.X * ThisItemScaleFactor,
//							childItem.itemBounds.Y - yf * itemOffset.Y * ThisItemScaleFactor),
//						childItem.itemBounds.Size);
//				}			

//				childItem.UpdateItemCanvasBounds();
//			}

//			// Updates link within this node
//			Node node = this as Node;
//			if (node != null)
//			{
//				node.UpdateLinksFor();
//			}

//			// Update links to or from this node (parent links)
//			Node parentNode = ParentItem as Node;
//			if (parentNode != null)
//			{
//				parentNode.UpdateLinksFor(this);
//			}
//		}


//		public void Resize(int zoomDelta, Point viewPosition)
//		{
//			double delta = (double)zoomDelta / 20.0;
//			xf = 1;
//			yf = 1;
//			wf = 2;
//			hf = 2;

//			Vector itemOffset = new Vector(delta / ItemScale, delta / ItemScale);

//			Point newItemLocation = new Point(
//				ItemBounds.X - xf * itemOffset.X,
//				ItemBounds.Y - yf * itemOffset.Y);

//			double width = ItemBounds.Size.Width + (wf * itemOffset.X);
//			double height = this.itemBounds.Size.Height + (hf * itemOffset.Y);

//			if (width < 0 || height < 0)
//			{
//				return;
//			}

//			Size newItemSize = new Size(width, height);
//			Rect newItemBounds = new Rect(newItemLocation, newItemSize);

//			if ((newItemBounds.X + newItemBounds.Width > ParentItem.ItemBounds.Width * ThisItemScaleFactor)
//					|| (newItemBounds.Y + newItemBounds.Height > ParentItem.ItemBounds.Height * ThisItemScaleFactor)
//					|| newItemBounds.X < 0
//					|| newItemBounds.Y < 0)
//			{
//				return;
//			}

//			ItemBounds = newItemBounds;
//			itemService.UpdateItem(this);
//			NotifyAll();


//			foreach (Item childItem in ChildItems)
//			{
//				// Resizing iten, ensure that child items do not move
//				childItem.ItemBounds = new Rect(
//					new Point(
//						childItem.ItemBounds.X + xf * itemOffset.X * ThisItemScaleFactor,
//						childItem.itemBounds.Y + yf * itemOffset.Y * ThisItemScaleFactor),
//					childItem.itemBounds.Size);

//				childItem.UpdateItemCanvasBounds();
//			}

//			// Updates link within this node
//			Node node = this as Node;
//			if (node != null)
//			{
//				node.UpdateLinksFor();
//			}

//			// Update links to or from this node (parent links)
//			Node parentNode = ParentItem as Node;
//			if (parentNode != null)
//			{
//				parentNode.UpdateLinksFor(this);
//			}
//		}


//		public void Zoom(int zoomDelta, Point viewPosition)
//		{
//			if (!childItems.Any(item => item.IsAdded))
//			{
//				return;
//			}


//			double zoom = Math.Pow(2, -zoomDelta / ZoomSpeed);

//			ThisItemScaleFactor *= zoom;

//			ItemBounds = ItemBounds;
//			itemService.UpdateItem(this);
//			NotifyAll();


//			foreach (Item childItem in ChildItems)
//			{
//				childItem.UpdateItemCanvasBounds();
//			}

//			// Updates link within this node
//			Node node = this as Node;
//			if (node != null)
//			{
//				node.UpdateLinksFor();
//			}

//			// Update links to or from this node (parent links)
//			Node parentNode = ParentItem as Node;
//			if (parentNode != null)
//			{
//				parentNode.UpdateLinksFor(this);
//			}
//		}


//		private void UpdateItemCanvasBounds()
//		{
//			ItemBounds = itemBounds;

//			if (IsAdded)
//			{
//				itemService.UpdateItem(this);
//				NotifyAll();
//			}

//			foreach (Item childNode in ChildItems)
//			{
//				childNode.UpdateItemCanvasBounds();
//			}
//		}

//		private void GetMoveResizeFactors(Point canvasPoint)
//		{
//			double xdist = Math.Abs(ItemCanvasBounds.X - canvasPoint.X);
//			double ydist = Math.Abs(ItemCanvasBounds.Y - canvasPoint.Y);

//			double wdist = Math.Abs(ItemCanvasBounds.Right - canvasPoint.X);
//			double hdist = Math.Abs(ItemCanvasBounds.Bottom - canvasPoint.Y);

//			double xd = xdist * CanvasScale;
//			double yd = ydist * CanvasScale;

//			double wd = wdist * CanvasScale;
//			double hd = hdist * CanvasScale;


//			if (ItemCanvasBounds.Width * CanvasScale > 80)
//			{
//				if (xd < 10 && yd < 10)
//				{
//					// Upper left corner (resize)
//					xf = 1;
//					yf = 1;
//					wf = -1;
//					hf = -1;
//				}
//				else if (wd < 10 && hd < 10)
//				{
//					// Lower rigth corner (resize)
//					xf = 0;
//					yf = 0;
//					wf = 1;
//					hf = 1;
//				}
//				else if (xd < 10 && hd < 10)
//				{
//					// Lower left corner (resize)
//					xf = 1;
//					yf = 0;
//					wf = -1;
//					hf = 1;
//				}
//				else if (wd < 10 && yd < 10)
//				{
//					// Upper rigth corner (resize)
//					xf = 0;
//					yf = 1;
//					wf = 1;
//					hf = -1;
//				}
//				else
//				{
//					// Inside rectangle, (normal mover)
//					xf = 1;
//					yf = 1;
//					wf = 0;
//					hf = 0;
//				}
//			}
//			else
//			{
//				// Rectangle to small to resize (normal mover)
//				xf = 1;
//				yf = 1;
//				wf = 0;
//				hf = 0;
//			}
//		}


//		protected virtual void ItemBoundsChanged()
//		{
//		}




//		internal IEnumerable<Item> GetHidableDecedentAndSelf()
//		{
//			if (IsAdded && !CanBeShown())
//			{
//				yield return this;

//				foreach (Item node in GetHidableDecedent())
//				{
//					yield return node;
//				}
//			}
//		}


//		internal IEnumerable<Item> GetHidableDecedent()
//		{
//			IEnumerable<Item> showableChildren = ChildItems
//				.Where(node => node.IsAdded && !node.CanBeShown());

//			foreach (Item childNode in showableChildren)
//			{
//				foreach (Item decedentNode in childNode.GetHidableDecedentAndSelf())
//				{
//					yield return decedentNode;
//				}
//			}
//		}


//		public Rect GetChildItemCanvasBounds(Rect childItemBounds)
//		{
//			double childItemScale = ItemScaleFactor / ThisItemScaleFactor;
//			childItemBounds.Scale(childItemScale, childItemScale);

//			return new Rect(
//				ItemCanvasBounds.X + (childItemBounds.X),
//				ItemCanvasBounds.Y + (childItemBounds.Y),
//				childItemBounds.Width,
//				childItemBounds.Height);	
//		}


//		protected void AddChildItem(Item child)
//		{
//			if (!childItems.Contains(child))
//			{
//				childItems.Add(child);
//			}

//			child.Priority = Priority - 0.1;

//			child.ZIndex = ZIndex + ((child is Link) ? 1 : 2);
//		}


//		//public void RemoveChildNode(Item child)
//		//{
//		//	childItems.Remove(child);
//		//	child.NotifyAll();
//		//}


//		public virtual void ChangedScale()
//		{
//			bool canBeShown = CanBeShown();

//			if (IsAdded)
//			{
//				if (!canBeShown)
//				{
//					HideNode();
//				}
//			}
//			else
//			{
//				if (canBeShown)
//				{
//					ShowItem();
//				}
//			}

//			if (IsAdded && IsRealized)
//			{
//				NotifyAll();

//				ChildItems
//					.ForEach(node => node.ChangedScale());
//			}
//		}
//	}
//}