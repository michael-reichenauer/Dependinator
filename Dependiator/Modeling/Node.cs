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
		private Rect relativeBounds;

		private readonly INodeService nodeService;

		protected Node(INodeService nodeService, Node parentNode)
		{
			this.nodeService = nodeService;
			ParentNode = parentNode;
		}

		public double Scale => nodeService.Scale;

		public Node ParentNode
		{
			get { return parentNode; }
			private set
			{
				parentNode = value;

				SetItemBounds();
			}
		}

		public IReadOnlyList<Node> ChildNodes => childNodes;

		public Rect RelativeBounds
		{
			get { return relativeBounds; }
			protected set
			{
				relativeBounds = value;
				SetItemBounds();
			}
		}


		public void ShowNode()
		{
			if (!IsAdded)
			{
				nodeService.ShowNode(this);

				ChildNodes.Where(node => !node.IsAdded)
					.ForEach(node => node.TryAddNode());
			}
		}


		public void HideNode()
		{
			if (IsAdded)
			{
				ChildNodes.Where(node => node.IsAdded)
					.ForEach(node => node.HideNode());

				nodeService.HideNode(this);
			}
		}


		public abstract void TryAddNode();

		private void SetItemBounds()
		{
			if (ParentNode != null)
			{
				ItemBounds = new Rect(
					ParentNode.ItemBounds.X + relativeBounds.X,
					ParentNode.ItemBounds.Y + relativeBounds.Y,
					relativeBounds.Width,
					relativeBounds.Height);
			}
			else
			{
				ItemBounds = relativeBounds;
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
			base.ChangedScale();

			ChildNodes
				.Where(node => !node.IsAdded)
				.ForEach(node => node.TryAddNode());
		}


		public override void Activated()
		{
			base.Activated();

			ChildNodes
			.Where(node => !node.IsAdded)
			.ForEach(node => node.TryAddNode());
		}
	}
}