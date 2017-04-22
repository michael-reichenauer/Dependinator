using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Media;
using Dependiator.Modeling.Items;
using Dependiator.Modeling.Links;
using Dependiator.Utils;


namespace Dependiator.Modeling.Nodes
{
	internal class Node : Equatable<Node>, IItemBounds
	{
		private const int InitialScaleFactor = 7;
		private readonly IItemService itemService;

		private readonly List<Node> childNodes = new List<Node>();

		private ItemsCanvas itemsCanvas;

		private Brush nodeBrush;

		private ItemViewModel viewModel;

		private bool IsShowing => IsRootNode || (viewModel?.IsShowing ?? false);
		private bool IsRootNode => ParentNode == null;


		public Node(
			IItemService itemService,
			Node parent,
			NodeName name,
			NodeType type)
		{
			this.itemService = itemService;
			ParentNode = parent;
			NodeName = name;
			NodeType = type;
			PersistentNodeColor = null;
			Links = new NodeLinks(itemService);
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
		public Point ItemsCanvasOffset => (Point)((Vector)ItemsOffset / ItemsScale);

		public NodeLinks Links { get; }

		public string PersistentNodeColor { get; set; }
		public Rect? PersistentNodeBounds { get; set; }
		public double? PersistentScale { get; set; }
		public Point? PersistentOffset { get; set; }


		public string DebugToolTip =>
			$"\n Children: {ChildNodes.Count} Shown Items: {CountShowingNodes()}\n" +
			$"Items Scale: {ItemsScale:0.00}, Scalefactor: {ItemsScaleFactor:0.00}\n" +
			$"Offset: {ItemsOffset.TS()}, CanvasOffset: {ItemsCanvasOffset.TS()}\n" +
			$"Rect: {NodeBounds.TS()}\n" +
			$"Pos in parent coord: {ParentNode?.itemsCanvas?.GetChildToParentCanvasPoint(NodeBounds.Location).TS()}\n" +
			$"Pos in child coord: {ParentNode?.itemsCanvas?.GetParentToChildCanvasPoint(ParentNode?.itemsCanvas?.GetChildToParentCanvasPoint(NodeBounds.Location) ?? new Point(0, 0)).TS()}\n" +
			$"Visual area {itemsCanvas?.ViewArea.TS()}\n" +
			$"Recursive viewArea {itemsCanvas?.GetVisualAncestorsArea().TS()}\n\n" +
			$"Parent {ParentNode?.NodeName}:{ParentNode?.DebugToolTip}";


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


		private int direction = 0;

		public void Move(Vector viewOffset, Point? viewPosition2, bool isDoing)
		{
			Vector scaledOffset = viewOffset / NodeScale;

			Point newLocation = NodeBounds.Location + scaledOffset;
			Size size = NodeBounds.Size;

			if (!isDoing)
			{
				direction = 0;
			}

			bool isMove = false;
			Vector move = new Vector();

			if ((viewPosition2.HasValue || direction > 0) && !(isDoing && direction == 0))
			{
				Point p = new Point(viewPosition2.Value.X / NodeScale, viewPosition2.Value.Y / NodeScale);
				double dist = 10 / NodeScale;
				double dist2 = 5 / NodeScale;

				if (Math.Abs(p.X - 0) < dist || direction == 1)
				{
					newLocation = new Point(NodeBounds.Location.X + scaledOffset.X, NodeBounds.Location.Y);
					size = new Size(size.Width - scaledOffset.X, size.Height);
					direction = 1;
					isMove = true;
					move = new Vector(viewOffset.X, 0);
				}
				else if (Math.Abs(p.X - NodeBounds.Width) < dist || direction == 2)
				{
					newLocation = NodeBounds.Location;
					size = new Size(size.Width + scaledOffset.X, size.Height);
					direction = 2;
				}
				else if (Math.Abs(p.Y - 0) < dist2 || direction == 3)
				{
					newLocation = new Point(NodeBounds.Location.X, NodeBounds.Location.Y + scaledOffset.Y);
					size = new Size(size.Width, size.Height - scaledOffset.Y);
					direction = 3;
					isMove = true;
					move = new Vector(0, viewOffset.Y);
				}
				else if (Math.Abs(p.Y - NodeBounds.Height) < dist || direction == 4)
				{
					newLocation = NodeBounds.Location;
					size = new Size(size.Width, size.Height + scaledOffset.Y);
					direction = 4;
				}
			}

			if (size.Width * NodeScale < 40 || size.Height * NodeScale < 20)
			{
				return;
			}

			NodeBounds = new Rect(newLocation, size);

			ParentNode.itemsCanvas.UpdateItem(viewModel);

			Links.ReferencingSegments.ForEach(segment => segment.UpdateVisibility());

			viewModel.NotifyAll();



			if (isMove)
			{
				MoveItems(-move);
			}
			else
			{
				UpdateShownItems();
				UpdateShownItemsInChildNodes();
			}
		}


		public void Resize(Vector viewOffset, Point? viewPosition2)
		{
			Vector scaledOffset = viewOffset / NodeScale;

			Point newLocation = NodeBounds.Location;
			Size size = NodeBounds.Size;

			if (viewPosition2.HasValue)
			{
				Point p = new Point(viewPosition2.Value.X / NodeScale, viewPosition2.Value.Y / NodeScale);
				double dist = 5 / NodeScale;

				if (Math.Abs(p.X - 0) < dist)
				{
					newLocation = new Point(NodeBounds.Location.X + scaledOffset.X, NodeBounds.Location.Y);
					size = new Size(size.Width - scaledOffset.X, size.Height);
				}
				else if (Math.Abs(p.X - NodeBounds.Width) < dist)
				{
					newLocation = NodeBounds.Location;
					size = new Size(size.Width + scaledOffset.X, size.Height);
				}

				if (Math.Abs(p.Y - 0) < dist)
				{
					newLocation = new Point(NodeBounds.Location.X, NodeBounds.Location.Y + scaledOffset.Y);
					size = new Size(size.Width, size.Height - scaledOffset.Y);
				}
				else if (Math.Abs(p.Y - NodeBounds.Height) < dist)
				{
					newLocation = NodeBounds.Location;
					size = new Size(size.Width, size.Height + scaledOffset.Y);
				}
			}

			if (size.Width * NodeScale < 40 || size.Height * NodeScale < 20)
			{
				return;
			}

			NodeBounds = new Rect(newLocation, size);

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
				.Where(segment => segment.Source == this || segment.Target == this)
				.ForEach(segment => segment.UpdateVisibility());

			UpdateShownItemsInChildNodes();
		}


		public void Resize(int wheelDelta)
		{
			double delta = (double)wheelDelta / 30;
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


		public void UpdateAllNodesScalesBeforeClose()
		{
			Stack<Node> nodes = new Stack<Node>();
			nodes.Push(this);

			while (nodes.Any())
			{
				Node node = nodes.Pop();

				if (node.ChildNodes.Any())
				{
					node.itemsCanvas.UpdateScale();
					node.ChildNodes.ForEach(child => nodes.Push(child));
				}
			}
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
				nodeBrush = itemService.GetBrushFromHex(PersistentNodeColor);
			}
			else
			{
				nodeBrush = itemService.GetRandomRectangleBrush();
				PersistentNodeColor = itemService.GetHexColorFromBrush(nodeBrush);
			}

			return nodeBrush;
		}


		public Brush GetBackgroundNodeBrush()
		{
			Brush brush = GetNodeBrush();
			return itemService.GetRectangleBackgroundBrush(brush);
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





		//private void ShowAllChildren()
		//{
		//	foreach (Node childNode in ChildNodes)
		//	{
		//		if (childNode.CanShowNode())
		//		{
		//			if (!childNode.viewModel.IsShowing)
		//			{
		//				childNode.viewModel.Show();
		//				childNode.ParentNode.UpdateShownItems();

		//				childNode.viewModel.NotifyAll();
		//			}
		//		}
		//	}

		//	UpdateShownItems();

		//	foreach (Node childNode in ChildNodes)
		//	{
		//		if (childNode.viewModel?.CanShow ?? false)
		//		{
		//			childNode.ShowAllChildren();
		//		}
		//	}
		//}


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
					count += node.Links.ManagedSegments.Count(l => l.ViewModel.IsShowing);
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
				itemService.SetChildrenLayout(this);

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

				double scale = PersistentScale ?? 0;
				if (Math.Abs(scale) > 0.001)
				{
					itemsCanvas.Scale = scale;
				}
				else
				{
					itemsCanvas.Scale = ParentNode.itemsCanvas.Scale / InitialScaleFactor;
				}

				Point offset = PersistentOffset ?? new Point(0, 0);
				itemsCanvas.Offset = offset;

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


		protected override bool IsEqual(Node other) => NodeName == other.NodeName;

		protected override int GetHash() => NodeName.GetHashCode();
	}
}