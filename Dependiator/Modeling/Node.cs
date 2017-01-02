using System.Collections.Generic;
using System.Linq;
using System.Windows;
using Dependiator.MainViews.Private;


namespace Dependiator.Modeling
{
	internal abstract class Node : Item
	{
		private Node parentNode;
		private readonly List<Node> childNodes = new List<Node>();
		private Rect actualNodeBounds;

		private readonly INodeService nodeService;

		protected Node(INodeService nodeService, Node parentNode)
		{
			this.nodeService = nodeService;
			ParentNode = parentNode;
		}

		public double Scale => nodeService.Scale;

		public IReadOnlyList<Node> ChildNodes => childNodes;

		public abstract bool CanBeShown();

		public Node ParentNode
		{
			get { return parentNode; }
			private set
			{
				parentNode = value;

				SetItemBounds();
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

		public Rect ViewNodeBounds => new Rect(
			actualNodeBounds.TopLeft, 
			new Size(actualNodeBounds.Width * Scale, actualNodeBounds.Height * Scale));


		public void ShowNode()
		{
			nodeService.ShowNodes(GetShowableNodes());			
		}


		public void HideNode()
		{
			nodeService.HideNodes(GetHidableNodes());
		}


		internal IEnumerable<Node> GetShowableNodes()
		{
			if (!IsAdded && CanBeShown())
			{
				yield return this;

				IEnumerable<Node> showableChildren = ChildNodes
					.Where(node => !node.IsAdded && node.CanBeShown());

				foreach (Node childNode in showableChildren)
				{
					foreach (Node decedentNode in childNode.GetShowableNodes())
					{
						yield return decedentNode;
					}
				}
			}
		}

		internal IEnumerable<Node> GetHidableNodes()
		{
			if (IsAdded && !CanBeShown())
			{
				yield return this;

				IEnumerable<Node> showableChildren = ChildNodes
					.Where(node => node.IsAdded && !node.CanBeShown());

				foreach (Node childNode in showableChildren)
				{
					foreach (Node decedentNode in childNode.GetHidableNodes())
					{
						yield return decedentNode;
					}
				}
			}
		}


		private void SetItemBounds()
		{
			if (ParentNode != null)
			{
				ItemBounds = new Rect(
					ParentNode.ItemBounds.X + actualNodeBounds.X,
					ParentNode.ItemBounds.Y + actualNodeBounds.Y,
					actualNodeBounds.Width,
					actualNodeBounds.Height);
			}
			else
			{
				ItemBounds = actualNodeBounds;
			}
		}


		protected void AddChild(Node child)
		{
			if (!childNodes.Contains(child))
			{
				childNodes.Add(child);
			}

			if (child.ParentNode != this)
			{
				child.ParentNode?.RemoveChild(child);
			}

			child.ParentNode = this;
			child.Priority = Priority - 0.1;
			child.ZIndex = ZIndex + 10;
		}


		private void RemoveChild(Node child)
		{
			childNodes.Remove(child);
			child.ParentNode = null;
		}

		public override void ChangedScale()
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

			if (IsAdded)
			{	
				base.ChangedScale();
				
				ChildNodes
					.ForEach(node => node.ChangedScale());
			}
		}


		//public override void Activated()
		//{
		//	base.Activated();

		//	ChildNodes
		//		.Where(node => !node.IsAdded)
		//		.ForEach(node => node.TryAddNode());
		//}
	}
}