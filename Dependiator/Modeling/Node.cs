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
		private const int InitialScaleFactor = 7;
		private readonly INodeItemService nodeItemService;

		private CompositeNodeViewModel compositeNodeViewModel;
		private SingleNodeViewModel singleNodeViewModel;
		private ItemsCanvas itemsCanvas;

		private Brush nodeBrush;

		private ItemViewModel currentViewModel;
		private bool isInitialized;

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
			NodeColor = null;
		}
	

		public Rect ItemBounds { get; set; }

		public double NodeItemScale => ParentNode?.itemsCanvas.Scale ?? 1.0;
		public double ItemsCanvasScale => itemsCanvas.Scale;



		public NodeName NodeName { get; }
		public NodeType NodeType { get; private set; }
		public Node ParentNode { get; }
		public List<Node> ChildNodes { get; } = new List<Node>();

		public NodeLinks Links { get; } = new NodeLinks();

		public string NodeColor { get; private set; }
		public Rect? NodeBounds { get; set; }


		private bool IsCompositeNodeView => currentViewModel is CompositeNodeViewModel;
		private bool IsCompositeNodeShowing => IsCompositeNodeView && currentViewModel.IsShowing;
		private bool IsSingleNodeView => currentViewModel is SingleNodeViewModel;
		private bool IsSingleNodeShowing => IsSingleNodeView && currentViewModel.IsShowing;

		public NodesView ParentView => ParentNode?.View;
		public NodesView View => compositeNodeViewModel?.NodesViewModel?.NodeView;


		public string ToolTip =>
			$"\n Children: {ChildNodes.Count} Items: {ItemViewModel.TotalCount}, {ItemsSource.ItemCount}\n" +
			$"Scale: {NodeItemScale:0.00}, ParentScale: {ParentNode.NodeItemScale:0.00}";


		public void Show(ItemsCanvas rootItemsCanvas)
		{
			Asserter.Requires(ParentNode == null);
		
			// Show children of the root noode
			itemsCanvas = rootItemsCanvas;
			UpdateVisibility();
		}


		public void Zoom(int zoomDelta, Point viewPosition)
		{
			double newScale = itemsCanvas.GetZoomScale(zoomDelta);
			if (!ChildNodes.Any(child => child.IsVisitableAtScale(newScale)))
			{
				return;
			}

			itemsCanvas.Zoom(zoomDelta, viewPosition);

			UpdateVisibility();
		}


		public void MoveChildren(Vector viewOffset)
		{
			itemsCanvas.Move(viewOffset);

			TriggerQueryInvalidatedInChildren();
		}


		public void MoveNode(Vector viewOffset)
		{
			Vector scaledOffset = viewOffset / NodeItemScale;
			ItemBounds = new Rect(ItemBounds.Location + scaledOffset, ItemBounds.Size);

			ParentNode.itemsCanvas.UpdateItem(singleNodeViewModel);

			if (compositeNodeViewModel != null)
			{
				ParentNode.itemsCanvas.UpdateItem(compositeNodeViewModel);
			}

			currentViewModel.NotifyAll();

			TriggerQueryInvalidated();
			TriggerQueryInvalidatedInChildren();
		}


		private void TriggerQueryInvalidated()
		{
			itemsCanvas?.TriggerInvalidated();
		}


		private void TriggerQueryInvalidatedInChildren()
		{
			ChildNodes
				.Where(node => node.IsCompositeNodeShowing)
				.ForEach(node =>
				{
					node.TriggerQueryInvalidated();
					node.TriggerQueryInvalidatedInChildren();
				});
		}


		public void Resize(int zoomDelta, Point viewPosition)
		{
			double delta = (double)zoomDelta / 5;

			double scaledDelta = delta / ParentNode.NodeItemScale;

			Point newItemLocation = new Point(
				ItemBounds.X - scaledDelta,
				ItemBounds.Y);

			double width = ItemBounds.Size.Width + (2 * scaledDelta);
			double height = ItemBounds.Size.Height + (2 * scaledDelta);

			if (width < 0 || height < 0)
			{
				return;
			}

			Size newItemSize = new Size(width, height);
			ItemBounds = new Rect(newItemLocation, newItemSize);

			ParentNode.itemsCanvas.UpdateItem(singleNodeViewModel);

			if (compositeNodeViewModel != null)
			{
				ParentNode.itemsCanvas.UpdateItem(compositeNodeViewModel);
			}

			currentViewModel.NotifyAll();
		}

		private async void UpdateVisibility()
		{
			InitNodeIfNeeded();

			UpdateScale();

			UpdateThisNodeVisibility();

			await Task.Yield();

			if (IsCompositeNodeView)
			{
				ChildNodes.ForEach(n => n.UpdateVisibility());
			}
		}


		private void UpdateScale()
		{
			if (IsCompositeNodeView)
			{
				compositeNodeViewModel.UpdateScale();
			}
		}


		private void UpdateThisNodeVisibility()
		{
			if (ParentNode != null && CanShowNode())
			{
				// Node is not shown and can be shown, Lets show it
				ShowNode();
			}
			else if (currentViewModel.CanShow)
			{
				// This node can no longer be shown, removing it and children are removed automatically
				currentViewModel.Hide();
			}
		}


		private void ShowNode()
		{
			if (CanShowChildren())
			{
				ShowCompositeNode();
			}
			else
			{
				ShowSingleNode();
			}

			currentViewModel.NotifyAll();
		}


		private void ShowCompositeNode()
		{
			if (IsCompositeNodeShowing)
			{
				// Already showing nodes node, lets just update scale
				compositeNodeViewModel.UpdateScale();
				return;
			}

			if (IsSingleNodeView)
			{
				// Switching from single to composite node
				singleNodeViewModel.Hide();			
			}

			compositeNodeViewModel.UpdateScale();
			compositeNodeViewModel.Show();

			currentViewModel = compositeNodeViewModel;

			// Notify parent item canvas that node view has changed
			ParentNode.TriggerQueryInvalidated();
		}



		private void ShowSingleNode()
		{
			if (IsSingleNodeShowing && currentViewModel.CanShow)
			{
				// Already showing nodes node, no need to change
				return;
			}

			if (IsCompositeNodeView)
			{
				// Switching from composite to single node
				compositeNodeViewModel.Hide();
			}

			singleNodeViewModel.Show();

			currentViewModel = singleNodeViewModel;

			// Notify parent item canvas that node view has changed
			ParentNode.TriggerQueryInvalidated();
		}


		public void NodeRealized()
		{
			ShowAllChildren();
		}

		public void NodeVirtualized()
		{
			HideAllChildren();

			foreach (Node childNode in ChildNodes)
			{
				childNode.ParentNodeVirtualized();
			}
		}


		public void ParentNodeVirtualized()
		{
			compositeNodeViewModel?.SetParentVirtualized();

			foreach (Node childNode in ChildNodes)
			{
				childNode.compositeNodeViewModel?.SetParentVirtualized();
				childNode.ParentNodeVirtualized();
			}
		}



		public void ShowAllChildren()
		{
			foreach (Node childNode in ChildNodes)
			{
				if (childNode.CanShowNode())
				{
					childNode.ShowNode();
				}
			}

			itemsCanvas?.TriggerInvalidated();

			foreach (Node childNode in ChildNodes)
			{
				if (childNode.currentViewModel?.CanShow ?? false)
				{
					childNode.ShowAllChildren();
				}
			}
		}


		public void HideAllChildren()
		{
			foreach (Node childNode in ChildNodes)
			{
				if (childNode.currentViewModel?.CanShow ?? false)
				{
					childNode.HideAllChildren();
					childNode.currentViewModel.Hide();
				}
			}

			itemsCanvas?.TriggerInvalidated();
		}


		public bool CanShowNode() => IsVisitableAtScale(NodeItemScale);


		private bool IsVisitableAtScale(double scale) => ItemBounds.Size.Width * scale > 40;


		private bool CanShowChildren()
		{
			//return ChildNodes.Any();

			if (IsCompositeNodeShowing)
			{
				return ChildNodes.Any(child => child.CanShowNode());
			}

			return ChildNodes
				.Any(child => child.ItemBounds.Size.Width * NodeItemScale / itemsCanvas.ScaleFactor > 40);

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


		private void InitNodeIfNeeded()
		{			
			if (isInitialized)
			{
				return;
			}

			Log.Debug($"Init {NodeName}");
			isInitialized = true;

			nodeItemService.SetChildrenLayout(this);

			if (ParentNode == null)
			{
				singleNodeViewModel = new SingleNodeViewModel(this);
			
				compositeNodeViewModel = new CompositeNodeViewModel(this, null);
				currentViewModel = compositeNodeViewModel;

				return;				
			}

			singleNodeViewModel = new SingleNodeViewModel(this);
			ParentNode.itemsCanvas.AddItem(singleNodeViewModel);

			if (ChildNodes.Any())
			{
				compositeNodeViewModel = new CompositeNodeViewModel(this, ParentNode.itemsCanvas);

				itemsCanvas = compositeNodeViewModel.ItemsCanvas;
				itemsCanvas.Scale = ParentNode.itemsCanvas.Scale / InitialScaleFactor;

				ParentNode.itemsCanvas.AddItem(compositeNodeViewModel);
			}

			currentViewModel = singleNodeViewModel;
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
	}
}