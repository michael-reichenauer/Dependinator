using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Media;
using Dependiator.Modeling.Items;
using Dependiator.Utils;
using Dependiator.Utils.UI;


namespace Dependiator.Modeling
{
	internal class NodeItem : IItem
	{
		private readonly Node node;

		public Rect ItemBounds { get; }
		public double Priority { get; }
		public ViewModel ViewModel { get; }
		public object ItemState { get; set; }

		public bool IsShown { get; public set; }


		public NodeItem(Node node)
		{
			this.node = node;
		}

		public void ItemRealized()
		{
			IsShown = true;
		}


		public void ItemVirtualized()
		{
			IsShown = false;
		}
	}


	internal class Node
	{
		private readonly INodeItemService nodeItemService;

		private double canvasScale = 1;
		private Brush nodeBrush;
		private ItemsCanvas nodesCanvas;
		private ViewModel viewModel;
		private NodesNodeViewModel nodesNodeViewModel;
		private NodeLeafViewModel nodeLeafViewModel;


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


		public double ScaleFactor { get; private set; } = 7;
		public double NodeScale => ParentNode?.NodeScale / ScaleFactor ?? canvasScale;
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
			nodesCanvas = rootCanvas;
			canvasScale = rootCanvas.Scale;

			UpdateOnScaleChange();
		}


		public void Zoom(int zoomDelta, Point viewPosition)
		{
			if (nodesCanvas == null)
			{
				return;
			}

			nodesCanvas.Zoom(zoomDelta, viewPosition);
			canvasScale = nodesCanvas.Scale;

			UpdateOnScaleChange();
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

			if (viewModel is NodesNodeViewModel nvm)
			{
				nvm.UpdateScale();
			}

			UpdateVisibility();

			if (viewModel is NodesNodeViewModel)
			{
				foreach (Node childNode in ChildNodes)
				{
					childNode.UpdateOnScaleChange();
				}
			}
		}


		public void UpdateVisibility()
		{
			bool canBeShown = CanBeShown();

			Log.Debug($"Update  {NodeName} IsShown={IsShown}, CanShow={canBeShown}, CanShowChildren?{CanShowChildren()}");

			if (!IsShown && canBeShown)
			{
				// Node is not shown and can be shown, Lets show it and check children as well
				Log.Debug($"Show {NodeName}");

				ParentNode.nodesCanvas?.AddItem(this);

				//				UpdateChildrenVisibility();
			}
			else if (IsShown && !canBeShown)
			{
				// This node can no longer be shown, removing it and children are removed automatically
				Log.Debug($"Hide {NodeName}");
				ParentNode.nodesCanvas?.RemoveItem(this);
				viewModel = null;
			}
			else if (IsShown && canBeShown)
			{
				Log.Debug($"Update {NodeName}");
				UpdateNode();
				// This node is shown and should continue the be shown, check children
				//				UpdateChildrenVisibility();
			}

			Log.Debug($"Updated {NodeName} IsShown={IsShown}, CanShow={canBeShown}, CanShowChildren?{CanShowChildren()}");
		}



		private void UpdateNode()
		{
			if (CanShowChildren())
			{
				if (!(viewModel is NodesNodeViewModel))
				{
					//if (nodesNodeViewModel == null)
					//{
					//	nodesNodeViewModel = new NodesNodeViewModel(this);
					//	nodesCanvas = nodesNodeViewModel.NodesCanvas;
					//}

					//viewModel = nodesNodeViewModel;
					//nodesNodeViewModel.UpdateScale();
					ItemBounds = new Rect(
						ItemBounds.X + 100, ItemBounds.Y + 100, ItemBounds.Size.Width, ItemBounds.Size.Height);
					ParentNode.nodesCanvas?.UpdateItem(this);
					viewModel.NotifyAll();
					Log.Warn($"Switch to nodes node for {NodeName}");
				}
				else
				{
					Log.Debug("No change of node type");
				}
			}
			else if (!CanShowChildren())
			{
				if (!(viewModel is NodeLeafViewModel))
				{
					if (nodeLeafViewModel == null)
					{
						nodeLeafViewModel = new NodeLeafViewModel(this);
						nodesCanvas = null;
					}

					viewModel = nodeLeafViewModel;
					ParentNode.nodesCanvas?.UpdateItem(this);
					viewModel.NotifyAll();

					Log.Warn($"Switch to leaf node for {NodeName}");
				}
				else
				{
					Log.Debug("No change of node type");
				}
			}
		}



		private ViewModel GetViewModel()
		{
			if (viewModel == null)
			{
				if (!CanBeShown())
				{
					Log.Debug($"Get No view model for {NodeName}");
					return null;
				}

				if (CanShowChildren())
				{
					Log.Debug($"Get Nodes node ViewModel for {NodeName}");

					if (nodesNodeViewModel == null)
					{
						nodesNodeViewModel = new NodesNodeViewModel(this);
						nodesCanvas = nodesNodeViewModel.NodesCanvas;
					}

					nodesNodeViewModel.UpdateScale();

					if (viewModel is NodeLeafViewModel)
					{
						Log.Warn($"Switch to nodes node for {NodeName}");
						viewModel = nodesNodeViewModel;
						nodesCanvas = nodesNodeViewModel.NodesCanvas;
						viewModel.NotifyAll();
					}
					else
					{
						viewModel = nodesNodeViewModel;
					}
				}
				else
				{
					Log.Debug($"Get Leaf node ViewModel for {NodeName}");

					if (nodeLeafViewModel == null)
					{
						nodeLeafViewModel = new NodeLeafViewModel(this);
					}

					if (viewModel is NodesNodeViewModel)
					{
						Log.Warn($"Switch to leaf node for {NodeName}");
						viewModel = nodeLeafViewModel;
						nodesCanvas = null;
						viewModel.NotifyAll();
					}
					else
					{
						viewModel = nodeLeafViewModel;
					}
				}
			}

			Log.Warn($"Get ViewModel for {NodeName}");
			return viewModel;
		}


		public bool CanBeShown()
		{
			return ItemBounds.Size.Width * ParentNode.NodeScale > 40;
			//ItemViewSize.Width > 10
			//&& (ParentItem?.ItemCanvasBounds.Contains(ItemCanvasBounds) ?? true);
		}

		private bool CanShowChildren()
		{
			return ChildNodes.Any() && ItemBounds.Size.Width * ParentNode.NodeScale > 400;
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
			if (!IsShown)
			{
				IsShown = true;
				Log.Warn($"Showing {NodeName} IsShown={IsShown}, CanShowChildren?{CanShowChildren()}");
			}
		}


		public void ItemVirtualized()
		{
			if (IsShown)
			{
				IsShown = false;
				Log.Warn($"Hiding {NodeName} IsShown={IsShown}, CanShowChildren?{CanShowChildren()}");
			}
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