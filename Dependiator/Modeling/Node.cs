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

		private readonly List<Node> childNodes = new List<Node>();

		private ItemsCanvas itemsCanvas;

		private Brush nodeBrush;

		private ItemViewModel viewModel;

		private bool IsShowing => IsRootNode || (viewModel?.IsShowing ?? false);
		private bool IsRootNode => ParentNode == null;


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

		public double NodeScale => ParentNode?.itemsCanvas.Scale ?? 1.0;
		public double ItemsScale => itemsCanvas?.Scale ?? 1;
		public double ItemsScaleFactor => itemsCanvas?.ScaleFactor ?? 1;
		public Point ItemsOffset => itemsCanvas?.Offset ?? new Point();

		public NodeName NodeName { get; }
		public NodeType NodeType { get; private set; }
		public Node ParentNode { get; }
		public IReadOnlyList<Node> ChildNodes => childNodes;

		public NodeLinks Links { get; } = new NodeLinks();

		public string NodeColor { get; private set; }
		public Rect? NodeBounds { get; set; }


		public string DebugToolTip =>
			$"\n Children: {ChildNodes.Count} Shown Items: {CountShowingNodes()}\n" +
			$"Items Scale: {ItemsScale:0.00}";


		public bool CanShowNode() => IsVisibleAtScale(NodeScale);

		private bool IsVisibleAtScale(double scale) => ItemBounds.Size.Width * scale > 40;


		public void Show(ItemsCanvas rootItemsCanvas)
		{
			Asserter.Requires(IsRootNode);
		
			itemsCanvas = rootItemsCanvas;
			DescendentsAndSelf().ForEach(child => child.Init());

			UpdateVisibility();
		}


		public void Zoom(double zoomFactor, Point zoomCenter)
		{		
			if (IsMinZoomLimit(zoomFactor))
			{
				return;
			}

			itemsCanvas.Zoom(zoomFactor, zoomCenter);

			UpdateVisibility();

			Links.ManagedSegments
				.Where(segment => segment.Source == this || segment.Target == this)
				.ForEach(segment => segment.UpdateVisibility());
		}


		public void Move(Vector viewOffset)
		{
			Vector scaledOffset = viewOffset / NodeScale;
			Point newLocation = ItemBounds.Location + scaledOffset;
			ItemBounds = new Rect(newLocation, ItemBounds.Size);

			ParentNode.itemsCanvas.UpdateItem(viewModel);

			Links.ReferencingSegments.ForEach(segment => segment.UpdateVisibility());

			viewModel.NotifyAll();

			UpdateShownItems();
			UpdateShownItemsInChildNodes();
		}


		public void MoveItems(Vector viewOffset)
		{
			if (!ChildNodes.Any(child => child.CanShowNode()))
			{
				// No children to move
				return;
			}

			itemsCanvas.Move(viewOffset);

			Links.ManagedSegments
				.Where(segment=> segment.Source == this || segment.Target == this)
				.ForEach(segment => segment.UpdateVisibility());

			UpdateShownItemsInChildNodes();
		}


		public void Resize(int wheelDelta)
		{
			double delta = (double)wheelDelta / 12;
			double scaledDelta = delta / NodeScale;
			
			double width = ItemBounds.Size.Width + (2 * scaledDelta);
			double height = ItemBounds.Size.Height + (2 * scaledDelta);
		
			if (width < 40 || height < 20)
			{
				return;
			}

			// double zoomFactor = width / ItemBounds.Size.Width;
			Point newLocation = new Point(ItemBounds.X - scaledDelta, ItemBounds.Y);
			Size newItemSize = new Size(width, height);
			ItemBounds = new Rect(newLocation, newItemSize);
	
			ParentNode.itemsCanvas.UpdateItem(viewModel);

			viewModel.NotifyAll();	


			MoveItems(new Vector(scaledDelta * NodeScale, 0));

			Links.ManagedSegments
				.Where(segment => segment.Target == this)
				.ForEach(segment => segment.UpdateVisibility());
		}


		private void UpdateShownItems()
		{
			itemsCanvas?.TriggerInvalidated();
		}


		private void UpdateShownItemsInChildNodes()
		{
			ChildNodes
				.Where(node => node.IsShowing)
				.ForEach(node =>
				{
					node.UpdateShownItems();
					node.UpdateShownItemsInChildNodes();
				});
		}


		private void UpdateVisibility()
		{
			if (itemsCanvas != null)
			{
				itemsCanvas.UpdateScale();
				viewModel.NotifyAll();
			}

			UpdateThisNodeVisibility();

			if (IsShowing)
			{
				ChildNodes.ForEach(child => child.UpdateVisibility());
				Links.ManagedSegments.ForEach(segment => segment.UpdateVisibility());
			}
		}


		private void UpdateThisNodeVisibility()
		{
			if (!IsRootNode && CanShowNode())
			{
				// Node is not shown and can be shown, Lets show it
				ShowNode();
			}
			else if (viewModel.CanShow)
			{
				// This node can no longer be shown, removing it and children are removed automatically
				viewModel.Hide();
			}
		}


		private void ShowNode()
		{
			if (viewModel.IsShowing)
			{
				return;
			}

			viewModel.Show();
			ParentNode.UpdateShownItems();

			viewModel.NotifyAll();
		}



		public void NodeRealized()
		{
			ShowAllChildren();
		}

		public void NodeVirtualized()
		{
			HideAllChildren();
		}



		private void ShowAllChildren()
		{
			foreach (Node childNode in ChildNodes)
			{
				if (childNode.CanShowNode())
				{
					childNode.ShowNode();
				}
			}

			UpdateShownItems();

			foreach (Node childNode in ChildNodes)
			{
				if (childNode.viewModel?.CanShow ?? false)
				{
					childNode.ShowAllChildren();
				}
			}
		}


		private void HideAllChildren()
		{
			foreach (Node childNode in ChildNodes)
			{
				if (childNode.viewModel?.CanShow ?? false)
				{
					childNode.HideAllChildren();
					childNode.viewModel.Hide();
				}
			}

			UpdateShownItems();
		}


		


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
			childNodes.Add(child);
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
			// Log.Debug("Counting shown nodes:");
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
					// Log.Debug($"  IsShowing {node}");
					node.ChildNodes.ForEach(nodes.Push);
				}
			}

			return count - 1;
		}


		private void Init()
		{			
			//Log.Debug($"Init {NodeName}");
			nodeItemService.SetChildrenLayout(this);

			if (ChildNodes.Any())
			{
				var compositeViewModel = new CompositeNodeViewModel(this, ParentNode?.itemsCanvas);
				viewModel = compositeViewModel;

				if (!IsRootNode)
				{
					itemsCanvas = compositeViewModel.ItemsCanvas;
					itemsCanvas.Scale = ParentNode.itemsCanvas.Scale / InitialScaleFactor;
				}

				itemsCanvas.AddItems(Links.ManagedSegments.Select(segment => segment.ViewModel));
			}
			else
			{
				viewModel = new SingleNodeViewModel(this);			
			}

			ParentNode?.itemsCanvas.AddItem(viewModel);
		}


		private bool IsMinZoomLimit(double zoomFactor)
		{
			double newScale = itemsCanvas.Scale * zoomFactor;

			return newScale < 1 && !ChildNodes.Any(child => child.IsVisibleAtScale(newScale));
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