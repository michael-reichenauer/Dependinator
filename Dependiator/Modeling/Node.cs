using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;
using Dependiator.Modeling.Items;
using Dependiator.Utils;


namespace Dependiator.Modeling
{
	internal class Node
	{
		private const int InitialScaleFactor = 7;
		private readonly INodeItemService nodeItemService;

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
		public double ItemsCanvasScale => itemsCanvas?.Scale ?? 1;
		public double ItemsScaleFactor => itemsCanvas?.ScaleFactor ?? 1;
		public Point ItemsOffset => itemsCanvas?.Offset ?? new Point();

		public NodeName NodeName { get; }
		public NodeType NodeType { get; private set; }
		public Node ParentNode { get; }
		public List<Node> ChildNodes { get; } = new List<Node>();

		public NodeLinks Links { get; } = new NodeLinks();

		public string NodeColor { get; private set; }
		public Rect? NodeBounds { get; set; }


		private bool IsShowing => IsRootNode || (currentViewModel?.IsShowing ?? false);
		private bool IsRootNode => ParentNode == null;

		private bool IsCompositeNodeView => currentViewModel is CompositeNodeViewModel;
		private bool IsCompositeNodeShowing => IsCompositeNodeView && currentViewModel.IsShowing;
	
		public NodesView ParentView => 
			((CompositeNodeViewModel)ParentNode.currentViewModel)?.NodesViewModel?.NodeView;


		public string ToolTip =>
			$"\n Children: {ChildNodes.Count} Shown Items: {CountShowingNodes()}\n" +
			$"Scale: {NodeItemScale:0.00}, ParentScale: {ParentNode.NodeItemScale:0.00}";
		


		public void Show(ItemsCanvas rootItemsCanvas)
		{
			Asserter.Requires(IsRootNode);
		
			// Show children of the root node
			itemsCanvas = rootItemsCanvas;
			InitAllNodes();

			UpdateVisibility();
		}


		private void InitAllNodes()
		{
			Timing t = new Timing();
			DescendentsAndSelf().ForEach(child => child.InitNodeIfNeeded());
			t.Log("Init of all nodes");
		}


		public void Zoom(double zoomFactor, Point zoomCenter)
		{
			double newScale = itemsCanvas.Scale * zoomFactor;
			if (newScale < 1 && !ChildNodes.Any(child => child.IsVisibleAtScale(newScale)))
			{
				return;
			}

			itemsCanvas.Zoom(zoomFactor, zoomCenter);

			UpdateVisibility();		
		}


		public void MoveNode(Vector viewOffset)
		{
			Vector scaledOffset = viewOffset / NodeItemScale;
			Point newLocation = ItemBounds.Location + scaledOffset;
			ItemBounds = new Rect(newLocation, ItemBounds.Size);

			ParentNode.itemsCanvas.UpdateItem(currentViewModel);

			Links.ReferencingSegments.ForEach(segment => segment.UpdateVisibility());

			currentViewModel.NotifyAll();

			TriggerQueryInvalidated();
			TriggerQueryInvalidatedInChildren();
		}


		public void MoveChildren(Vector viewOffset)
		{
			if (!ChildNodes.Any(child => child.CanShowNode()))
			{
				// No children to move
				return;
			}

			itemsCanvas.Move(viewOffset);
			Links.ManagedSegments.ForEach(segment => segment.UpdateVisibility());

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


		public void ZoomResize(int wheelDelta)
		{
			double delta = (double)wheelDelta / 12;

			double scaledDelta = delta / NodeItemScale;

			Point newItemLocation = new Point(ItemBounds.X - scaledDelta, ItemBounds.Y);

			double width = ItemBounds.Size.Width + (2 * scaledDelta);
			double height = ItemBounds.Size.Height + (2 * scaledDelta);

			// double zoom = width / ItemBounds.Size.Width;
			if (width < 40 || height < 20)
			{
				return;
			}

			Size newItemSize = new Size(width, height);
			ItemBounds = new Rect(newItemLocation, newItemSize);

		
			ParentNode.itemsCanvas.UpdateItem(currentViewModel);

			currentViewModel.NotifyAll();

			Links.ManagedSegments.ForEach(segment => segment.UpdateVisibility());

			TriggerQueryInvalidatedInChildren();

			MoveChildren(new Vector(scaledDelta * NodeItemScale, 0));
		}


		private void UpdateVisibility()
		{
			//InitNodeIfNeeded();

			UpdateScale();

			UpdateThisNodeVisibility();

			//await Task.Yield();

			if (IsRootNode || IsCompositeNodeShowing)
			{
				ChildNodes.ForEach(child => child.UpdateVisibility());
				Links.ManagedSegments.ForEach(segment => segment.UpdateVisibility());
			}
		}


		private void UpdateScale()
		{
			if (currentViewModel is  CompositeNodeViewModel compositeViewModel)
			{
				compositeViewModel.UpdateScale();
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
			if (currentViewModel.IsShowing)
			{
				return;
			}

			currentViewModel.Show();
			ParentNode.TriggerQueryInvalidated();

			currentViewModel.NotifyAll();
		}



		public void NodeRealized()
		{
			ShowAllChildren();
		}

		public void NodeVirtualized()
		{
			HideAllChildren();
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


		public bool CanShowNode() => IsVisibleAtScale(NodeItemScale);


		private bool IsVisibleAtScale(double scale) => ItemBounds.Size.Width * scale > 40;


		//private bool CanShowChildren()
		//{
		//	return ChildNodes.Any();

		//	//if (IsCompositeNodeShowing)
		//	//{
		//	//	return ChildNodes.Any(child => child.CanShowNode());
		//	//}

		//	//return ChildNodes
		//	//	.Any(child => child.ItemBounds.Size.Width * NodeItemScale / itemsCanvas.ScaleFactor > 40);

		//}


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


		public void UpdateItem(ItemViewModel viewModel)
		{
			itemsCanvas.UpdateItem(viewModel);
		}


		private int CountShowingNodes()
		{
			Log.Debug("Counting shown nodes:");
			Stack<Node> nodes = new Stack<Node>();

			Node startNode = this;
			while (!startNode.IsRootNode)
			{
				startNode = startNode.ParentNode;
			}

			nodes.Push(startNode);

			int count = 0;
			while (nodes.Any())
			{
				Node node = nodes.Pop();
				if (node.IsShowing)
				{
					count++;
					Log.Debug($"  IsShowing {node}");
					node.ChildNodes.ForEach(nodes.Push);
				}
			}

			return count - 1;
		}


			private void InitNodeIfNeeded()
		{			
			if (isInitialized)
			{
				return;
			}

			//Log.Debug($"Init {NodeName}");
			isInitialized = true;

			nodeItemService.SetChildrenLayout(this);


			if (ChildNodes.Any())
			{
				var compositeViewModel = new CompositeNodeViewModel(this, ParentNode?.itemsCanvas);
				currentViewModel = compositeViewModel;

				if (!IsRootNode)
				{
					itemsCanvas = compositeViewModel.ItemsCanvas;
					itemsCanvas.Scale = ParentNode.itemsCanvas.Scale / InitialScaleFactor;
				}

				itemsCanvas.AddItems(Links.ManagedSegments.Select(segment => segment.ViewModel));
			}
			else
			{
				currentViewModel = new SingleNodeViewModel(this);			
			}

			ParentNode?.itemsCanvas.AddItem(currentViewModel);
		}


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