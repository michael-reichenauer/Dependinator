using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
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

		private CompositeNodeViewModel compositeNodeViewModel;
		private NodeLeafViewModel leafNodeViewModel;

		private Brush nodeBrush;
		private ItemsCanvas rootNodesCanvas;
		private ItemViewModel currentViewModel;
		private bool isAdded;

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


		public bool IsShown => currentViewModel?.IsShown ?? false;

		public double ScaleFactor = 7;
		public double NodeScale => ParentNode?.NodeScale / ScaleFactor ?? rootNodesCanvas.Scale;

		public double ChildScale => compositeNodeViewModel?.IsShown ?? false ? compositeNodeViewModel.Scale : NodeScale;

		public ItemsCanvas ItemsCanvas => rootNodesCanvas ?? compositeNodeViewModel?.ItemsCanvas;

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
			Asserter.Requires(ParentNode == null);

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

		public void Move(Vector viewOffset)
		{
			if (ParentNode == null)
			{
				rootNodesCanvas.Move(viewOffset);
			}
		}


		private async void UpdateOnScaleChange()
		{
			if (ParentNode == null)
			{
				foreach (Node childNode in ChildNodes)
				{
					childNode.UpdateOnScaleChange();
					await Task.Yield();
				}

				// Root node is not visible, lets exit
				return;
			}

			if (!isAdded)
			{
				Log.Debug($"Add {NodeName}");
				AddNodeAsChildToParent();
				isAdded = true;
			}

			UpdateScale();

			UpdateVisibility();

			//// Enable next row when everything works !!!!!!!!!!!!!!!!!!!
			if (currentViewModel is CompositeNodeViewModel)
			{
				foreach (Node childNode in ChildNodes)
				{
					childNode.UpdateOnScaleChange();
					await Task.Yield();
				}
			}
		}


		private void AddNodeAsChildToParent()
		{
			leafNodeViewModel = new NodeLeafViewModel(this);
			ParentNode.AddChildItem(leafNodeViewModel);
			currentViewModel = leafNodeViewModel;
			currentViewModel.IsVisible = false;

			if (ChildNodes.Any())
			{
				Log.Debug($"Add composite node for {NodeName}");
				compositeNodeViewModel = new CompositeNodeViewModel(this, ParentNode?.ItemsCanvas);
				ParentNode.AddChildItem(compositeNodeViewModel);
			}
		}


		private void UpdateScale()
		{
			if (currentViewModel is CompositeNodeViewModel)
			{
				compositeNodeViewModel.UpdateScale();
			}
		}


		public void UpdateVisibility()
		{
			if (CanBeShown())
			{
				// Node is not shown and can be shown, Lets show it
				ShowNode();
			}
			else if (currentViewModel.IsVisible)
			{
				// This node can no longer be shown, removing it and children are removed automatically
				currentViewModel.IsVisible = false;
			}
		}


		private void ShowNode()
		{
			if (CanShowChildren())
			{
				CompositeNode();
			}
			else
			{
				ShowLeafNode();
			}

			currentViewModel.NotifyAll();
		}


		private void CompositeNode()
		{
			if (currentViewModel is CompositeNodeViewModel && currentViewModel.IsVisible)
			{
				// Already showing nodes node, lets just update scale
				compositeNodeViewModel.UpdateScale();
				return;
			}

			if (currentViewModel is NodeLeafViewModel)
			{
				// SWitching from leaf to nodes node
				leafNodeViewModel.IsVisible = false;
			}

			compositeNodeViewModel.UpdateScale();
			compositeNodeViewModel.IsVisible = true;

			currentViewModel = compositeNodeViewModel;

			// Notify parent item canvas that node view has changed
			ParentNode.ItemsCanvas.TriggerInvalidated();
		}


		private void ShowLeafNode()
		{
			if (currentViewModel is NodeLeafViewModel && currentViewModel.IsVisible)
			{
				// Already showing nodes node, no need to change
				return;
			}

			if (currentViewModel is CompositeNodeViewModel)
			{
				// SWitching from nodes node to leaf
				compositeNodeViewModel.IsVisible = false;
			}

			leafNodeViewModel.IsVisible = true;

			currentViewModel = leafNodeViewModel;

			// Notify parent item canvas that node view has changed
			ParentNode.ItemsCanvas.TriggerInvalidated();
		}


		private void AddChildItem(IItem item)
		{
			Log.Debug($"Add item {item}");
			if (ParentNode == null)
			{
				rootNodesCanvas.AddItem(item);
			}
			else
			{
				compositeNodeViewModel.AddItem(item);
			}
		}


		public void ShowAllChildren()
		{
			foreach (Node childNode in ChildNodes)
			{
				if (childNode.CanBeShown())
				{
					childNode.ShowNode();
				}
			}

			ItemsCanvas?.TriggerInvalidated();

			foreach (Node childNode in ChildNodes)
			{
				if (childNode.currentViewModel?.IsVisible ?? false)
				{
					childNode.ShowAllChildren();
				}
			}
		}


		public void HideAllChildren()
		{
			foreach (Node childNode in ChildNodes)
			{
				if (childNode.currentViewModel?.IsVisible ?? false)
				{
					childNode.HideAllChildren();
					childNode.currentViewModel.IsVisible = false;
				}
			}

			ItemsCanvas?.TriggerInvalidated();
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