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
		private Rect nodeBounds;

		private readonly INodeService nodeService;

		private int xf = 1;
		private int yf = 1;
		private int wf = 0;
		private int hf = 0;

		public Rect ItemCanvasBounds { get; protected set; }
		public double ZIndex { get; set; }
		public double Priority { get; protected set; }
		public abstract ViewModel ViewModel { get; }
		public object ItemState { get; set; }

		public bool IsAdded => ItemState != null || ParentNode == null;
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
		

		protected Node(INodeService nodeService, Node parentNode)
		{
			this.nodeService = nodeService;
			ParentNode = parentNode;
		}


		public int NodeLevel => ParentNode?.NodeLevel + 1 ?? 0;

		public double ThisNodeScaleFactor { get; set; } = 7;

		public double CanvasScale => nodeService.Scale;

		public double NodeScale => CanvasScale * NodeScaleFactor;

		public double NodeScaleFactor
		{
			get
			{
				if (ParentNode == null)
				{
					return ThisNodeScaleFactor;
				}

				return ParentNode.NodeScaleFactor / ThisNodeScaleFactor;
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


		public Rect NodeBounds
		{
			get { return nodeBounds; }
			set
			{
				nodeBounds = value;

				SetItemBounds();

				SetElementBounds();
			}
		}


		public Rect RelativeNodeBounds { get; private set; }


		public Rect ViewNodeBounds => new Rect(new Point(0, 0), ViewNodeSize);


		public Size ViewNodeSize =>
			new Size(ItemCanvasBounds.Width * CanvasScale, ItemCanvasBounds.Height * CanvasScale);


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


		public void MoveOrResize(Point canvasPoint, Vector viewOffset, bool isFirst)
		{
			if (isFirst)
			{
				GetMoveResizeFactors(canvasPoint);
			}

			Vector offset = new Vector(viewOffset.X / NodeScale, viewOffset.Y / NodeScale);

			Point location = new Point(
				NodeBounds.X + xf * offset.X,
				NodeBounds.Y + yf * offset.Y);

			double width = NodeBounds.Size.Width + (wf * offset.X);
			double height = this.nodeBounds.Size.Height + (hf * offset.Y);

			if (width < 0 || height < 0)
			{
				return;
			}

			Size size = new Size(width, height);

			Rect nodeBounds = new Rect(location, size);

			if ((nodeBounds.X + nodeBounds.Width > ParentNode.NodeBounds.Width * ThisNodeScaleFactor)
			    || (nodeBounds.Y + nodeBounds.Height > ParentNode.NodeBounds.Height * ThisNodeScaleFactor)
			    || nodeBounds.X < 0
			    || nodeBounds.Y < 0)
			{
				return;
			}

			NodeBounds = nodeBounds;

			nodeService.UpdateNode(this);

			NotifyAll();

			//double cxf = 0;
			//if (xf == 1 && offset.X < 0)
			//{
			//	cxf = 1;
			//}

		

			foreach (Node childNode in ChildNodes)
			{
				//Vector childOffset = new Vector(
				//	(offset.X) * NodeScaleFactor * ((1 / ThisNodeScaleFactor) / ThisNodeScaleFactor),
				//	(offset.Y) * NodeScaleFactor * ((1 / ThisNodeScaleFactor) / ThisNodeScaleFactor));

				Vector childOffset = new Vector(
					offset.X * childNode.NodeScale, 
					offset.Y * childNode.NodeScale);

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


		protected virtual void SetElementBounds()
		{
		}


		private void MoveAsChild(Vector offset)
		{
			NodeBounds = new Rect(
				new Point(
					NodeBounds.X + offset.X,
					NodeBounds.Y + offset.Y),
				NodeBounds.Size);

			if (IsAdded)
			{
				nodeService.UpdateNode(this);
				NotifyAll();
			}

			Vector childOffset = new Vector(offset.X / ThisNodeScaleFactor, offset.Y / ThisNodeScaleFactor);

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
				Rect bounds = nodeBounds;
				bounds.Scale(NodeScaleFactor, NodeScaleFactor);

				ItemCanvasBounds = new Rect(
					ParentNode.ItemCanvasBounds.X + (bounds.X),
					ParentNode.ItemCanvasBounds.Y + (bounds.Y),
					bounds.Width,
					bounds.Height);

				Rect bounds2 = nodeBounds;
				bounds2.Scale(1 / ThisNodeScaleFactor, 1 / ThisNodeScaleFactor);

				RelativeNodeBounds = bounds2;
			}
			else
			{
				ItemCanvasBounds = nodeBounds;
				RelativeNodeBounds = nodeBounds;
			}
		}


		protected void AddChildNode(Node child)
		{
			if (!childNodes.Contains(child))
			{
				childNodes.Add(child);
			}

			child.Priority = Priority - 0.1;

			child.ZIndex = ZIndex + ((child is Link) ? 1 : 2);
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