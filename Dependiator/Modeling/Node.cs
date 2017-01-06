using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using Dependiator.MainViews;
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
		

		public object ItemState { get; set; }
		public bool IsAdded => ItemState != null;
		public abstract ViewModel ViewModel { get; }

		public Rect ItemBounds { get; protected set; }
		public int ZIndex { get; protected set; }
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
					return 1;
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
			}
		}

		public Rect ActualNodeBoundsNoScale
		{
			get { return actualNodeBounds; }
			set
			{
				actualNodeBounds = value;
				SetItemBounds();
			}
		}

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


		//internal IEnumerable<Node> GetShowableNodes()
		//{
		//	if (!IsAdded && CanBeShown())
		//	{
		//		yield return this;

		//		IEnumerable<Node> showableChildren = ChildNodes
		//			.Where(node => !node.IsAdded && node.CanBeShown());

		//		foreach (Node childNode in showableChildren)
		//		{
		//			foreach (Node decedentNode in childNode.GetShowableNodes())
		//			{
		//				yield return decedentNode;
		//			}
		//		}
		//	}
		//}

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
			}
			else
			{
				ItemBounds = actualNodeBounds;
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