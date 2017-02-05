using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using Dependiator.MainViews.Private;
using Dependiator.Utils.UI;


namespace Dependiator.Modeling
{
	internal abstract class Item : IItem
	{
		private Item parentItem;
		private readonly List<Item> childItems = new List<Item>();
		private Rect itemBounds;

		private readonly IItemService itemService;

		private int xf = 1;
		private int yf = 1;
		private int wf = 0;
		private int hf = 0;

		protected Item(IItemService itemService, Item parentItem)
		{
			this.itemService = itemService;
			ParentItem = parentItem;
		}

		public Rect ItemCanvasBounds { get; protected set; }
		public double ZIndex { get; set; }
		public double Priority { get; protected set; }
		public abstract ViewModel ViewModel { get; }
		public object ItemState { get; set; }

		public bool IsAdded => ItemState != null || ParentItem == null;
		public bool IsRealized { get; private set; }


		public void NotifyAll()
		{
			if (IsAdded)
			{
				ViewModel.NotifyAll();
			}
		}

		public virtual void ItemRealized() => IsRealized = true;
		
		public virtual void ItemVirtualized() => IsRealized = false;
		


		public int ItemLevel => ParentItem?.ItemLevel + 1 ?? 0;

		public double ThisItemScaleFactor { get; set; } = 7;

		public double CanvasScale => itemService.CanvasScale;

		public double ItemScale => CanvasScale * ItemScaleFactor;

		public double ItemScaleFactor
		{
			get
			{
				if (ParentItem == null)
				{
					return ThisItemScaleFactor;
				}

				return ParentItem.ItemScaleFactor / ThisItemScaleFactor;
			}
		}

		public IReadOnlyList<Item> ChildItems => childItems;

		public abstract bool CanBeShown();

		public Item ParentItem
		{
			get { return parentItem; }
			set
			{
				parentItem = value;

				if (parentItem != null)
				{
					parentItem.AddChildItem(this);
				}
			}
		}


		public Rect ItemBounds
		{
			get { return itemBounds; }
			set
			{
				itemBounds = value;
				ItemCanvasBounds = ParentItem?.GetChildItemCanvasBounds(itemBounds) ?? itemBounds;

				ItemBoundsChanged();
			}
		}


		public Size ItemViewSize =>
			new Size(ItemCanvasBounds.Width * CanvasScale, ItemCanvasBounds.Height * CanvasScale);


		public void ShowNode()
		{
			if (!IsAdded && CanBeShown())
			{
				itemService.ShowItem(this);
			}
		}


		public void ShowChildren()
		{
			ChildItems.ForEach(node => node.ShowNode());
		}


		public void HideNode()
		{
			itemService.HideItems(GetHidableDecedentAndSelf());
		}


		public void HideChildren()
		{
			itemService.HideItems(GetHidableDecedent());
		}


		public void MoveOrResize(Point canvasPoint, Vector viewOffset, bool isFirst)
		{
			if (isFirst)
			{
				GetMoveResizeFactors(canvasPoint);
			}

			Vector offset = new Vector(viewOffset.X / ItemScale, viewOffset.Y / ItemScale);

			Point location = new Point(
				ItemBounds.X + xf * offset.X,
				ItemBounds.Y + yf * offset.Y);

			double width = ItemBounds.Size.Width + (wf * offset.X);
			double height = this.itemBounds.Size.Height + (hf * offset.Y);

			if (width < 0 || height < 0)
			{
				return;
			}

			Size size = new Size(width, height);

			Rect nodeBounds = new Rect(location, size);

			if ((nodeBounds.X + nodeBounds.Width > ParentItem.ItemBounds.Width * ThisItemScaleFactor)
			    || (nodeBounds.Y + nodeBounds.Height > ParentItem.ItemBounds.Height * ThisItemScaleFactor)
			    || nodeBounds.X < 0
			    || nodeBounds.Y < 0)
			{
				return;
			}

			ItemBounds = nodeBounds;

			itemService.UpdateItem(this);

			NotifyAll();

			//double cxf = 0;
			//if (xf == 1 && offset.X < 0)
			//{
			//	cxf = 1;
			//}

		

			foreach (Item childNode in ChildItems)
			{
				//Vector childOffset = new Vector(
				//	(offset.X) * ItemScaleFactor * ((1 / ThisItemScaleFactor) / ThisItemScaleFactor),
				//	(offset.Y) * ItemScaleFactor * ((1 / ThisItemScaleFactor) / ThisItemScaleFactor));

				Vector childOffset = new Vector(
					offset.X * childNode.ItemScale, 
					offset.Y * childNode.ItemScale);

				childNode.MoveAsChild(childOffset);
			}

			Node node = this as Node;
			if (node != null)
			{
				node.UpdateLinksFor();
			}

			Node parentNode = ParentItem as Node;
			if (parentNode != null)
			{
				parentNode.UpdateLinksFor(this);
			}
		}


		private void GetMoveResizeFactors(Point canvasPoint)
		{
			double xdist = Math.Abs(ItemCanvasBounds.X - canvasPoint.X);
			double ydist = Math.Abs(ItemCanvasBounds.Y - canvasPoint.Y);

			double wdist = Math.Abs(ItemCanvasBounds.Right - canvasPoint.X);
			double hdist = Math.Abs(ItemCanvasBounds.Bottom - canvasPoint.Y);

			double xd = xdist * CanvasScale;
			double yd = ydist * CanvasScale;

			double wd = wdist * CanvasScale;
			double hd = hdist * CanvasScale;


			if (ItemCanvasBounds.Width * CanvasScale > 80)
			{
				if (xd < 10 && yd < 10)
				{
					// Upper left corner (resize)
					xf = 1;
					yf = 1;
					wf = -1;
					hf = -1;
				}
				else if (wd < 10 && hd < 10)
				{
					// Lower rigth corner (resize)
					xf = 0;
					yf = 0;
					wf = 1;
					hf = 1;
				}
				else if (xd < 10 && hd < 10)
				{
					// Lower left corner (resize)
					xf = 1;
					yf = 0;
					wf = -1;
					hf = 1;
				}
				else if (wd < 10 && yd < 10)
				{
					// Upper rigth corner (resize)
					xf = 0;
					yf = 1;
					wf = 1;
					hf = -1;
				}
				else
				{
					// Inside rectangle, (normal mover)
					xf = 1;
					yf = 1;
					wf = 0;
					hf = 0;
				}
			}
			else
			{
				// Rectangle to small to resize (normal mover)
				xf = 1;
				yf = 1;
				wf = 0;
				hf = 0;
			}
		}


		protected virtual void ItemBoundsChanged()
		{
		}


		private void MoveAsChild(Vector offset)
		{
			ItemBounds = new Rect(
				new Point(
					ItemBounds.X + offset.X,
					ItemBounds.Y + offset.Y),
				ItemBounds.Size);

			if (IsAdded)
			{
				itemService.UpdateItem(this);
				NotifyAll();
			}

			Vector childOffset = new Vector(offset.X / ThisItemScaleFactor, offset.Y / ThisItemScaleFactor);

			foreach (Item childNode in ChildItems)
			{
				childNode.MoveAsChild(childOffset);
			}
		}


		internal IEnumerable<Item> GetHidableDecedentAndSelf()
		{
			if (IsAdded && !CanBeShown())
			{
				yield return this;

				foreach (Item node in GetHidableDecedent())
				{
					yield return node;
				}
			}
		}


		internal IEnumerable<Item> GetHidableDecedent()
		{
			IEnumerable<Item> showableChildren = ChildItems
				.Where(node => node.IsAdded && !node.CanBeShown());

			foreach (Item childNode in showableChildren)
			{
				foreach (Item decedentNode in childNode.GetHidableDecedentAndSelf())
				{
					yield return decedentNode;
				}
			}
		}


		private Rect GetChildItemCanvasBounds(Rect childItemBounds)
		{
			double childItemScale = ItemScaleFactor / ThisItemScaleFactor;
			childItemBounds.Scale(childItemScale, childItemScale);

			return new Rect(
				ItemCanvasBounds.X + (childItemBounds.X),
				ItemCanvasBounds.Y + (childItemBounds.Y),
				childItemBounds.Width,
				childItemBounds.Height);	
		}


		protected void AddChildItem(Item child)
		{
			if (!childItems.Contains(child))
			{
				childItems.Add(child);
			}

			child.Priority = Priority - 0.1;

			child.ZIndex = ZIndex + ((child is Link) ? 1 : 2);
		}


		public void RemoveChildNode(Item child)
		{
			childItems.Remove(child);
			child.NotifyAll();
		}


		public virtual void ChangedScale()
		{
			bool canBeShown = CanBeShown();

			if (IsAdded)
			{
				if (!canBeShown)
				{
					HideNode();
				}
			}
			else
			{
				if (canBeShown)
				{
					ShowNode();
				}
			}

			if (IsAdded && IsRealized)
			{
				NotifyAll();

				ChildItems
					.ForEach(node => node.ChangedScale());
			}
		}
	}
}