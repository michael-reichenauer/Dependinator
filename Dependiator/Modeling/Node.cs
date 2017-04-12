using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;
using Dependiator.Modeling.Items;
using Dependiator.Utils;


namespace Dependiator.Modeling
{
	internal class Node : IItemBounds
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
			PersistentNodeColor = null;
		}


		public NodeName NodeName { get; }
		public NodeType NodeType { get; private set; }
		public Rect NodeBounds { get; set; }
		public double NodeScale => ParentNode?.itemsCanvas.Scale ?? 1.0;

		public Node ParentNode { get; }

		public IReadOnlyList<Node> ChildNodes => childNodes;
		public double ItemsScale => itemsCanvas?.Scale ?? 1;
		public double ItemsScaleFactor => itemsCanvas?.ScaleFactor ?? 1;
		public Point ItemsOffset => itemsCanvas?.Offset ?? new Point();

		public NodeLinks Links { get; } = new NodeLinks();

		public string PersistentNodeColor { get; private set; }
		public Rect? PersistentNodeBounds { get; set; }


		public string DebugToolTip =>
			$"\n Children: {ChildNodes.Count} Shown Items: {CountShowingNodes()}\n" +
			$"Items Scale: {ItemsScale:0.00}";


		public bool CanShowNode() => IsVisibleAtScale(NodeScale);



		public void Show(ItemsCanvas rootItemsCanvas)
		{
			Asserter.Requires(IsRootNode);
		
			InitNodeTree(rootItemsCanvas);

			UpdateNodeVisibility();
		}


		public void Zoom(double zoomFactor, Point? zoomCenter = null)
		{		
			if (IsMinZoomLimit(zoomFactor))
			{
				return;
			}

			itemsCanvas.Zoom(zoomFactor, zoomCenter);

			UpdateNodeVisibility();

			Links.ManagedSegments
				.Where(segment => segment.Source == this || segment.Target == this)
				.ForEach(segment => segment.UpdateVisibility());
		}


		public void Move(Vector viewOffset)
		{
			Vector scaledOffset = viewOffset / NodeScale;
			Point newLocation = NodeBounds.Location + scaledOffset;
			NodeBounds = new Rect(newLocation, NodeBounds.Size);

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
			
			double width = NodeBounds.Size.Width + (2 * scaledDelta);
			double height = NodeBounds.Size.Height + (2 * scaledDelta);
		
			if (width < 40 || height < 20)
			{
				return;
			}

			double zoomFactor = width / NodeBounds.Size.Width;
			Point newLocation = new Point(NodeBounds.X - scaledDelta, NodeBounds.Y);
			Size newItemSize = new Size(width, height);
			NodeBounds = new Rect(newLocation, newItemSize);
	
			ParentNode.itemsCanvas.UpdateItem(viewModel);

			viewModel.NotifyAll();	

			Zoom(zoomFactor);

			//MoveItems(new Vector(scaledDelta * NodeScale, 0));

			//Links.ManagedSegments
			//	.Where(segment => segment.Target == this)
			//	.ForEach(segment => segment.UpdateVisibility());
		}



		public void NodeRealized()
		{
			UpdateNodeVisibility();
		}

		public void NodeVirtualized()
		{
			HideAllChildren();
		}



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

			if (PersistentNodeColor != null)
			{
				nodeBrush = nodeItemService.GetBrushFromHex(PersistentNodeColor);
			}
			else
			{
				nodeBrush = nodeItemService.GetRandomRectangleBrush();
				PersistentNodeColor = nodeItemService.GetHexColorFromBrush(nodeBrush);
			}

			return nodeBrush;
		}


		public Brush GetBackgroundNodeBrush()
		{
			Brush brush = GetNodeBrush();
			return nodeItemService.GetRectangleBackgroundBrush(brush);
		}


		public void UpdateItem(ItemViewModel itemViewModel)
		{
			itemsCanvas.UpdateItem(itemViewModel);
		}

		private bool IsVisibleAtScale(double scale) => NodeBounds.Size.Width * scale > 40;


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

	

		private void UpdateNodeVisibility()
		{
			IEnumerable<Node> childrenToUpdate = Enumerable.Empty<Node>();

			if (ChildNodes.Any())
			{
				itemsCanvas.UpdateScale();

				var childrenToShow = ChildNodes
					.Where(child => !child.viewModel.CanShow && child.CanShowNode())
					.Select(child => child.viewModel);

				var childrenToHide = ChildNodes
					.Where(child => child.viewModel.CanShow && !child.CanShowNode())
					.Select(child => child.viewModel);

				childrenToUpdate = ChildNodes
					.Where(child => child.viewModel.CanShow && child.viewModel.IsShowing && child.CanShowNode())
					.ToList();

				var segmentsToShow = Links.ManagedSegments
					.Where(segment => !segment.ViewModel.CanShow && segment.CanBeShown())
					.Select(segment => segment.ViewModel);

				var segmentsToHide = Links.ManagedSegments
					.Where(segment => segment.ViewModel.CanShow && !segment.CanBeShown())
					.Select(segment => segment.ViewModel);
		

				var itemsToShow = childrenToShow.Concat(segmentsToShow);
				var itemsToHide = childrenToHide.Concat(segmentsToHide);

				itemsToShow.ForEach(item => item.Show());
				itemsToHide.ForEach(item => item.Hide());

				var itemsToUpdate = itemsToHide.Concat(itemsToShow).ToList();

				itemsCanvas.UpdateItems(itemsToUpdate);
			}

			if (IsShowing)
			{
				viewModel.NotifyAll();

				childrenToUpdate.ForEach(child => child.UpdateNodeVisibility());
				Links.ManagedSegments.ForEach(segment => segment.UpdateVisibility());
			}
		}





		private void ShowAllChildren()
		{
			foreach (Node childNode in ChildNodes)
			{
				if (childNode.CanShowNode())
				{
					if (!childNode.viewModel.IsShowing)
					{
						childNode.viewModel.Show();
						childNode.ParentNode.UpdateShownItems();

						childNode.viewModel.NotifyAll();
					}
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


		private void InitNodeTree(ItemsCanvas rootCanvas)
		{
			itemsCanvas = rootCanvas;
			viewModel = new CompositeNodeViewModel(this, rootCanvas);

			InitNode();
		}


		private void InitNode()
		{
			if (ChildNodes.Any())
			{
				nodeItemService.SetChildrenLayout(this);

				var childViewModels = ChildNodes.Select(childNode => childNode.CreateViewModel());
				var segmentViewModels = Links.ManagedSegments.Select(segment => segment.ViewModel);

				var itemViewModels = segmentViewModels.Concat(childViewModels);
				itemsCanvas.AddItems(itemViewModels);

				ChildNodes.ForEach(childNode => childNode.InitNode());
			}		
		}


		private ItemViewModel CreateViewModel()
		{
			if (ChildNodes.Any())
			{
				itemsCanvas = new ItemsCanvas(this, ParentNode.itemsCanvas);
				itemsCanvas.Scale = ParentNode.itemsCanvas.Scale / InitialScaleFactor;
			
				viewModel = new CompositeNodeViewModel(this, itemsCanvas);
			}
			else
			{
				viewModel = new SingleNodeViewModel(this);
			}

			return viewModel;
		}


		private bool IsMinZoomLimit(double zoomFactor)
		{
			double newScale = itemsCanvas.Scale * zoomFactor;

			return zoomFactor < 1 && !ChildNodes.Any(child => child.IsVisibleAtScale(newScale));
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