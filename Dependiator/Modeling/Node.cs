using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using Dependiator.MainViews.Private;
using Dependiator.Utils.UI;


namespace Dependiator.Modeling
{
	internal abstract class Node : IItem
	{
		private Node parentNode;
		private readonly List<Node> childNodes = new List<Node>();
		private Rect actualNodeBounds;

		private readonly INodeService nodeService;

		private int xf = 1;
		private int yf = 1;
		private int wf = 0;
		private int hf = 0;

		public object ItemState { get; set; }
		public bool IsAdded => ItemState != null || ParentNode == null;
		public abstract ViewModel ViewModel { get; }

		public Rect ItemBounds { get; protected set; }
		public int ZIndex { get; set; }
		public double Priority { get; protected set; }

		public bool IsRealized { get; private set; }


		public void NotifyAll()
		{
			if (IsAdded)
			{
				ViewModel.NotifyAll();
			}
		}


		public virtual void ItemRealized()
		{
			IsRealized = true;
		}


		public virtual void ItemVirtualized()
		{
			IsRealized = false;
		}


		protected Node(INodeService nodeService, Node parentNode)
		{
			this.nodeService = nodeService;
			ParentNode = parentNode;
		}


		public int NodeLevel => ParentNode?.NodeLevel + 1 ?? 0;

		public double NodeScaleFactor { get; set; } = 7;

		public double Scale => nodeService.Scale;
		public double ViewScale => Scale * NodeScale;

		public double NodeScale
		{
			get
			{
				if (ParentNode == null)
				{
					return NodeScaleFactor;
				}

				return ParentNode.NodeScale / NodeScaleFactor;
			}
		}

		public IReadOnlyList<Node> ChildNodes => childNodes;

		public abstract bool CanBeShown();

		public Node ParentNode
		{
			get { return parentNode; }
			set
			{
				parentNode = value;

				if (parentNode != null)
				{
					parentNode.AddChildNode(this);
				}
			}
		}


		public Rect ActualNodeBounds
		{
			get { return actualNodeBounds; }
			set
			{
				actualNodeBounds = value;

				SetItemBounds();

				SetElementBounds();
			}
		}


		public Rect RelativeNodeBounds { get; set; }


		public Rect ViewNodeBounds => new Rect(new Point(0, 0), ViewNodeSize);


		public Size ViewNodeSize =>
			new Size(ItemBounds.Width * Scale, ItemBounds.Height * Scale);


		public void ShowNode()
		{
			if (!IsAdded && CanBeShown())
			{
				nodeService.ShowNode(this);
			}
		}


		public void ShowChildren()
		{
			ChildNodes.ForEach(node => node.ShowNode());
		}


		public void HideNode()
		{
			nodeService.HideNodes(GetHidableDecedentAndSelf());
		}


		public void HideChildren()
		{
			nodeService.HideNodes(GetHidableDecedent());
		}


		public void Move(Vector viewOffset)
		{
			Vector offset = new Vector(
				(viewOffset.X / NodeScale) / Scale,
				(viewOffset.Y / NodeScale) / Scale);


			Rect nodeBounds = new Rect(
				new Point(
					ActualNodeBounds.X + offset.X,
					ActualNodeBounds.Y + offset.Y),
				ActualNodeBounds.Size);

			if ((nodeBounds.X + nodeBounds.Width > ParentNode.ActualNodeBounds.Width * NodeScaleFactor)
			    || (nodeBounds.Y + nodeBounds.Height > ParentNode.ActualNodeBounds.Height * NodeScaleFactor)
			    || nodeBounds.X < 0
			    || nodeBounds.Y < 0)
			{
				return;
			}

			ActualNodeBounds = nodeBounds;

			nodeService.UpdateNode(this);

			NotifyAll();

			Vector childOffset = new Vector(offset.X * NodeScale, offset.Y * NodeScale);

			foreach (Node childNode in ChildNodes)
			{
				childNode.MoveAsChild(childOffset);
			}

			Module parentModule = ParentNode as Module;
			if (parentModule != null)
			{
				parentModule.UpdateLinksFor(this);
			}
		}


		public void Resize(Point canvasPoint, Vector viewOffset, bool isFirst)
		{
			if (isFirst)
			{
				double xdist = Math.Abs(ItemBounds.X - canvasPoint.X);
				double ydist = Math.Abs(ItemBounds.Y - canvasPoint.Y);

				double wdist = Math.Abs(ItemBounds.Right - canvasPoint.X);
				double hdist = Math.Abs(ItemBounds.Bottom - canvasPoint.Y);

				double xd = xdist * Scale;
				double yd = ydist * Scale;

				double wd = wdist * Scale;
				double hd = hdist * Scale;


				if (ItemBounds.Width * Scale > 80)
				{
					if (xd < 10 && yd < 10)
					{
						xf = 1;
						yf = 1;
						wf = -1;
						hf = -1;
					}
					else if (wd < 10 && hd < 10)
					{
						xf = 0;
						yf = 0;
						wf = 1;
						hf = 1;
					}
					else if (xd < 10 && hd < 10)
					{
						xf = 1;
						yf = 0;
						wf = -1;
						hf = 1;
					}
					else if (wd < 10 && yd < 10)
					{
						xf = 0;
						yf = 1;
						wf = 1;
						hf = -1;
					}
					else if (xd < 10)
					{
						xf = 1;
						yf = 0;
						wf = -1;
						hf = 0;
					}
					else if (yd < 10)
					{
						xf = 0;
						yf = 1;
						wf = 0;
						hf = -1;
					}
					else if (wd < 10)
					{
						xf = 0;
						yf = 0;
						wf = 1;
						hf = 0;
					}
					else if (hd < 10)
					{
						xf = 0;
						yf = 0;
						wf = 0;
						hf = 1;
					}
					else
					{
						xf = 1;
						yf = 1;
						wf = 0;
						hf = 0;
					}
				}
				else
				{
					xf = 1;
					yf = 1;
					wf = 0;
					hf = 0;
				}
			}
			else
			{
				
			}

			Vector offset = new Vector(
				(viewOffset.X / NodeScale) / Scale,
				(viewOffset.Y / NodeScale) / Scale);

			Point location = new Point(
				ActualNodeBounds.X + xf * offset.X,
				ActualNodeBounds.Y + yf * offset.Y);


			double width = ActualNodeBounds.Size.Width + (wf * offset.X);
			double height = actualNodeBounds.Size.Height + (hf * offset.Y);

			if (width < 0 || height < 0)
			{
				return;
			}

			Size size = new Size(width, height);

			Rect nodeBounds = new Rect(location, size);

			if ((nodeBounds.X + nodeBounds.Width > ParentNode.ActualNodeBounds.Width * NodeScaleFactor)
			    || (nodeBounds.Y + nodeBounds.Height > ParentNode.ActualNodeBounds.Height * NodeScaleFactor)
			    || nodeBounds.X < 0
			    || nodeBounds.Y < 0)
			{
				return;
			}

			ActualNodeBounds = nodeBounds;

			nodeService.UpdateNode(this);

			NotifyAll();

			Vector childOffset = new Vector(
				offset.X * NodeScale * ((1 / NodeScaleFactor) / NodeScaleFactor), 
				offset.Y * NodeScale * ((1 / NodeScaleFactor) / NodeScaleFactor));

			foreach (Node childNode in ChildNodes)
			{
				childNode.MoveAsChild(childOffset);
			}

			Module module = this as Module;
			if (module != null)
			{
				module.UpdateLinksFor();
			}

			Module parentModule = ParentNode as Module;
			if (parentModule != null)
			{
				parentModule.UpdateLinksFor(this);
			}
		}


		protected virtual void SetElementBounds()
		{
		}


		private void MoveAsChild(Vector offset)
		{
			ActualNodeBounds = new Rect(
				new Point(
					ActualNodeBounds.X + offset.X,
					ActualNodeBounds.Y + offset.Y),
				ActualNodeBounds.Size);

			if (IsAdded)
			{
				nodeService.UpdateNode(this);
				NotifyAll();
			}

			Vector childOffset = new Vector(offset.X / NodeScaleFactor, offset.Y / NodeScaleFactor);

			foreach (Node childNode in ChildNodes)
			{
				childNode.MoveAsChild(childOffset);
			}
		}


		internal IEnumerable<Node> GetHidableDecedentAndSelf()
		{
			if (IsAdded && !CanBeShown())
			{
				yield return this;

				foreach (Node node in GetHidableDecedent())
				{
					yield return node;
				}
			}
		}


		internal IEnumerable<Node> GetHidableDecedent()
		{
			IEnumerable<Node> showableChildren = ChildNodes
				.Where(node => node.IsAdded && !node.CanBeShown());

			foreach (Node childNode in showableChildren)
			{
				foreach (Node decedentNode in childNode.GetHidableDecedentAndSelf())
				{
					yield return decedentNode;
				}
			}
		}


		private void SetItemBounds()
		{
			if (ParentNode != null)
			{
				Rect bounds = actualNodeBounds;
				bounds.Scale(NodeScale, NodeScale);

				ItemBounds = new Rect(
					ParentNode.ItemBounds.X + (bounds.X),
					ParentNode.ItemBounds.Y + (bounds.Y),
					bounds.Width,
					bounds.Height);

				Rect bounds2 = actualNodeBounds;
				bounds2.Scale(1 / NodeScaleFactor, 1 / NodeScaleFactor);

				RelativeNodeBounds = bounds2;
			}
			else
			{
				ItemBounds = actualNodeBounds;
				RelativeNodeBounds = actualNodeBounds;
			}
		}


		protected void AddChildNode(Node child)
		{
			if (!childNodes.Contains(child))
			{
				childNodes.Add(child);
			}

			child.Priority = Priority - 0.1;
			child.ZIndex = ZIndex + 10;
		}


		public void RemoveChildNode(Node child)
		{
			childNodes.Remove(child);
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

				ChildNodes
					.ForEach(node => node.ChangedScale());
			}
		}
	}
}