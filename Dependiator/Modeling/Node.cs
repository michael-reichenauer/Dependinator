using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Media;
using Dependiator.Modeling.Items;
using Dependiator.Utils;
using Dependiator.Utils.UI;


namespace Dependiator.Modeling
{
	internal class Node
	{
		private readonly INodeItemService nodeItemService;

		private Brush nodeBrush;
		private ItemsCanvas rootNodesCanvas;
		private ItemViewModel viewModel;
		private NodesNodeViewModel nodesNodeViewModel;
		private NodeLeafViewModel leafNodeViewModel;


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

		public NodesViewModel NodesViewModel;


		public bool IsShown => viewModel?.IsShown ?? false;

		public double ScaleFactor = 7;
		public double NodeScale => ParentNode?.NodeScale / ScaleFactor ?? rootNodesCanvas.Scale;

		public double ChildScale => nodesNodeViewModel?.IsShown?? false ? nodesNodeViewModel.Scale : NodeScale;

		public Node ParentNode { get; }
		public NodeName NodeName { get; }
		public NodeType NodeType { get; private set; }
		//public NodeLinks Links { get; }
		// public List<Link> LinkItems => ChildItems.OfType<Link>();
		public string NodeColor { get; private set; }
		public Rect? NodeBounds { get; set; }
		public List<Node> ChildNodes { get; } = new List<Node>();


		public void SetBounds(Rect bounds)
		{
			ItemBounds = bounds;

			nodeItemService.SetChildrenItemBounds(this);
		}


		public void SetRootCanvas(ItemsCanvas rootCanvas)
		{
			rootNodesCanvas = rootCanvas;

			UpdateOnScaleChange();
		}


		public void Zoom(int zoomDelta, Point viewPosition)
		{
			if (ParentNode == null)
			{
				rootNodesCanvas.Zoom(zoomDelta, viewPosition);
			}

			UpdateOnScaleChange();
		}


		public void AddChildItem(IItem item)
		{
			if (ParentNode == null)
			{
				rootNodesCanvas.AddItem(item);
			}
			else
			{
				nodesNodeViewModel?.AddItem(item);
			}			
		}

		public void RemoveChildItem(IItem item)
		{
			if (ParentNode == null)
			{
				rootNodesCanvas.RemoveItem(item);
			}
			else
			{
				nodesNodeViewModel?.RemoveItem(item);
			}		
		}


		private void UpdateOnScaleChange()
		{
			if (ParentNode == null)
			{
				foreach (Node childNode in ChildNodes)
				{
					childNode.UpdateOnScaleChange();
				}

				// Root node is not visible, lets exit
				return;
			}

			UpdateScale();

			UpdateVisibility();

			if (viewModel is NodesNodeViewModel)
			{
				foreach (Node childNode in ChildNodes)
				{
					childNode.UpdateOnScaleChange();
				}
			}
		}


		private void UpdateScale()
		{
			if (viewModel is NodesNodeViewModel nvm)
			{
				nvm.UpdateScale();
			}
		}


		public void UpdateVisibility()
		{
			bool canBeShown = CanBeShown();

			if (canBeShown)
			{
				// Node is not shown and can be shown, Lets show it
				ShowNode();
			}
			else if (IsShown)
			{
				// This node can no longer be shown, removing it and children are removed automatically
				HideNode();
			}
		}


		private void ShowNode()
		{
			if (CanShowChildren())
			{
				ShowNodesNode();
			}
			else
			{
				ShowLeafNode();
			}

			viewModel.NotifyAll();
		}


		private void ShowNodesNode()
		{
			if (viewModel is NodeLeafViewModel)
			{
				HideNode();
			}

			EnsureNodesNodeViewModel();

			nodesNodeViewModel.UpdateScale();

			ShowNode(nodesNodeViewModel);
		}


		private void ShowLeafNode()
		{
			if (viewModel is NodesNodeViewModel)
			{
				HideNode();
			}

			EnsureLeafNodeViewModel();

			ShowNode(leafNodeViewModel);
		}


		private void EnsureNodesNodeViewModel()
		{
			if (nodesNodeViewModel == null)
			{
				nodesNodeViewModel = new NodesNodeViewModel(this);
			}
		}


		private void EnsureLeafNodeViewModel()
		{
			if (leafNodeViewModel == null)
			{
				leafNodeViewModel = new NodeLeafViewModel(this);
			}
		}


		private void ShowNode(ItemViewModel itemViewModel)
		{
			if (viewModel != itemViewModel || !itemViewModel.IsShown)
			{
				viewModel = itemViewModel;
				ParentNode?.AddChildItem(viewModel);
			}
		}


		private void HideNode()
		{
			if (viewModel != null)
			{
				ParentNode?.RemoveChildItem(viewModel);
				viewModel = null;
			}
		}


		public bool CanBeShown()
		{
			return ItemBounds.Size.Width * ParentNode.ChildScale > 40;
			//ItemViewSize.Width > 10
			//&& (ParentItem?.ItemCanvasBounds.Contains(ItemCanvasBounds) ?? true);
		}



		private bool CanShowChildren()
		{
			//return ChildNodes.Any() && ItemBounds.Size.Width * NodeScale >= (200 / ScaleFactor);
			return ChildNodes.Any(child => child.CanBeShown());
		}


		public void SetType(NodeType nodeType)
		{
			NodeType = nodeType;
		}


		public void AddChild(Node child)
		{
			ChildNodes.Add(child);
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