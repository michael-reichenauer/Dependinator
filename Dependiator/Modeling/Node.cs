using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Media;
using Dependiator.Modeling.Items;
using Dependiator.Utils.UI;


namespace Dependiator.Modeling
{
	internal class Node : IItem
	{
		private readonly INodeItemService nodeItemService;

		private double canvasScale = 1;
		private Brush nodeBrush;
		private ViewModel viewModel;
		private NodesNodeViewModel nodesNodeViewModel;
		private NodeLeafViewModel nodeLeafViewModel;
		private bool isShown = false;

		public Node(
			INodeItemService nodeItemService,
			Node parent,
			NodeName name,
			NodeType type)
		{
			this.nodeItemService = nodeItemService;
			ParentNode = parent;
			NodeName = name;
			NodeType = type;
			//Links = new NodeLinks(this);
			NodeColor = null;
		}

		public Rect ItemBounds { get; private set; }
		public double Priority { get; private set; } = 0;
		public ViewModel ViewModel => GetViewModel();

		public NodesViewModel NodesViewModel;

		public object ItemState { get; set; }
		public double ScaleFactor { get; private set; } = 7;



		public double NodeScale
		{
			get
			{
				if (ParentNode == null)
				{
					return canvasScale;
				}

				return ParentNode.NodeScale / ScaleFactor;
			}
		}


		public Node ParentNode { get; }

		public NodeName NodeName { get; }

		public NodeType NodeType { get; private set; }

		//	public NodeLinks Links { get; }

		public string NodeColor { get; private set; }

		public Rect? NodeBounds { get; set; }



		public List<Node> ChildNodes { get; } = new List<Node>();

		//public List<Link> LinkItems => ChildItems.OfType<Link>();



		public void SetScale(Double scale)
		{
			canvasScale = scale;
			UpdateScale();
		}



		public void UpdateScale()
		{
			if (NodesViewModel != null && ParentNode != null)
			{
				NodesViewModel.Scale = NodeScale;
				ViewModel.NotifyAll();
			}

			foreach (Node childNode in ChildNodes)
			{
				childNode.UpdateScale();
			}

			UpdateVisibility();
		}



		public void UpdateVisibility()
		{
			if (ParentNode == null)
			{
				UpdateChildrenVisibility();
				return;
			}

			bool canBeShown = CanBeShown();

			if (!isShown && canBeShown)
			{
				// Node is not shown and can be shown, Lets show it and check children as well
				ParentNode.NodesViewModel.AddItem(this);
				isShown = true;

				UpdateChildrenVisibility();
			}
			else if (isShown && !canBeShown)
			{
				// This node can no longer be shown, removing it and children are removed automatically
				ParentNode.NodesViewModel.RemoveItem(this);
				isShown = false;
			}
			else if (isShown && canBeShown)
			{
				// This node is shown and should continue the be shown, check children
				UpdateChildrenVisibility();
			}
		}


		private void UpdateChildrenVisibility()
		{
			foreach (Node childNode in ChildNodes)
			{
				childNode.UpdateVisibility();
			}
		}


		private ViewModel GetViewModel()
		{
			if (viewModel == null)
			{
				if (!CanBeShown())
				{
					return null;
				}

				if (ChildNodes.Any())
				{
					if (nodeLeafViewModel == null)
					{
						nodesNodeViewModel = new NodesNodeViewModel(this);
						NodesViewModel = nodesNodeViewModel.NodesViewModel;
					}

					viewModel = nodesNodeViewModel;
				}
				else
				{
					if (nodeLeafViewModel == null)
					{
						nodeLeafViewModel = new NodeLeafViewModel(this);
					}		

					viewModel = nodeLeafViewModel;
				}
			}

			return viewModel;
		}


		public void SetBounds(Rect bounds)
		{
			ItemBounds = bounds;

			nodeItemService.SetChildrenItemBounds(this);
		}


		public bool CanBeShown()
		{
			return ItemBounds.Size.Width * ParentNode.NodeScale > 40;
			//ItemViewSize.Width > 10
			//&& (ParentItem?.ItemCanvasBounds.Contains(ItemCanvasBounds) ?? true);
		}

		public void SetType(NodeType nodeType)
		{
			NodeType = nodeType;
		}


		public void AddChild(Node child)
		{
			ChildNodes.Add(child);
		}


		public void ItemRealized()
		{
			//if (!IsRealized)
			//{
			//	base.ItemRealized();

			//	if (!isAdded)
			//	{
			//		isAdded = true;
			//		AddModuleChildren();
			//		AddLinks();
			//	}

			//	ShowChildren();
			//}
		}



		public void ChangedScale()
		{
			//base.ChangedScale();
		}


		public void ItemVirtualized()
		{
			//if (IsRealized)
			//{
			//	HideChildren();
			//	base.ItemVirtualized();
			//	//ParentNode?.RemoveChildNode(this);
			//}
		}


		public override string ToString() => NodeName;

		public IEnumerable<Node> Ancestors()
		{
			Node current = ParentNode;

			while (current != null)
			{
				yield return current;
				current = current.ParentNode;
			}
		}

		public IEnumerable<Node> AncestorsAndSelf()
		{
			yield return this;

			foreach (Node ancestor in Ancestors())
			{
				yield return ancestor;
			}
		}

		public IEnumerable<Node> Descendents()
		{
			foreach (Node child in ChildNodes)
			{
				yield return child;

				foreach (Node descendent in child.Descendents())
				{
					yield return descendent;
				}
			}
		}

		public IEnumerable<Node> DescendentsAndSelf()
		{
			yield return this;

			foreach (Node descendent in Descendents())
			{
				yield return descendent;
			}
		}


		public Brush GetNodeBrush()
		{
			if (nodeBrush != null)
			{
				return nodeBrush;
			}

			if (NodeColor != null)
			{
				nodeBrush = nodeItemService.GetBrushFromHex(NodeColor);
			}
			else
			{
				nodeBrush = nodeItemService.GetRandomRectangleBrush();
				NodeColor = nodeItemService.GetHexColorFromBrush(nodeBrush);
			}

			return nodeBrush;
		}


		public Brush GetBackgroundNodeBrush()
		{
			Brush brush = GetNodeBrush();
			return nodeItemService.GetRectangleBackgroundBrush(brush);
		}


		//private void AddLinks()
		//{
		//	foreach (Link link in Links)
		//	{
		//		AddLink(link);
		//	}
		//}


		//private void AddLink(Link link)
		//{
		//	if (link.Source == this)
		//	{	
		//		AddChildItem(link);
		//		link.UpdateLinkLine();
		//	}
		//}


		//public void UpdateLinksFor(Item item)
		//{
		//	IEnumerable<Link> links = Links
		//		.Where(link => link.Source == item || link.Target == item)
		//		.ToList();

		//	foreach (Link link in links)
		//	{
		//		link.UpdateLinkLine();
		//		link.NotifyAll();
		//	}
		//}


		//public void UpdateLinksFor()
		//{
		//	IEnumerable<Link> links = Links	
		//		.ToList();

		//	foreach (Link link in links)
		//	{
		//		link.UpdateLinkLine();
		//		link.NotifyAll();
		//	}
		//}

	}
}