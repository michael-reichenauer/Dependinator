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

		public Rect ItemBounds { get; private set; }
		public double Priority { get; private set; }
		public ViewModel ViewModel { get; private set; }
		public object ItemState { get; set; }


		public double ScaleFactor { get; private set; } = 7;
		private double canvasScale = 1;


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
		}


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


		public NodeViewModel ModuleViewModel => ViewModel as NodeViewModel;
		public Brush RectangleBrush { get; private set; }
		public Brush BackgroundBrush { get; private set; }

		public List<Node> ChildNodes { get; } = new List<Node>();

		//public List<Link> LinkItems => ChildItems.OfType<Link>();

		public Rect? ElementBounds { get; set; }


		public void Zoom(Double scale)
		{
			canvasScale = scale;
			UpdateScale();
		}



		public void UpdateScale()
		{
			if (ViewModel is NodeWithChildrenViewModel vm)
			{
				vm.UpdateZoomScale();
			}

			foreach (Node childNode in ChildNodes)
			{
				childNode.UpdateScale();
			}
		}


		public void SetBounds(Rect bounds)
		{
			ItemBounds = bounds;

			RectangleBrush = nodeItemService.GetRectangleBrush();
			BackgroundBrush = nodeItemService.GetRectangleBackgroundBrush(RectangleBrush);

			if (ChildNodes.Any())
			{
				NodeWithChildrenViewModel nodeWithChildrenViewModel = new NodeWithChildrenViewModel(this);
				ViewModel = nodeWithChildrenViewModel;
				nodeItemService.AddModuleChildren(this, nodeWithChildrenViewModel.NodesViewModel);
			}
			else
			{
				ViewModel = new NodeLeafViewModel(this);
			}
		}


		public bool CanBeShown()
		{
			return true;
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