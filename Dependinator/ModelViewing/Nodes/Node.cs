using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Media;
using Dependinator.Modeling;
using Dependinator.ModelViewing.Items;
using Dependinator.ModelViewing.Links;
using Dependinator.Utils;


namespace Dependinator.ModelViewing.Nodes
{
	internal class Node : Equatable<Node>, IItemBounds
	{
		private const int InitialScaleFactor = 7;
		private readonly IModelViewService modelViewService;
		private readonly INodeService nodeService;

		private readonly List<Node> childNodes = new List<Node>();

		private ItemsCanvas itemsCanvas;

		private Brush nodeBrush;

		private ItemViewModel viewModel;

		private bool IsShowing => IsRootNode || (viewModel?.IsShowing ?? false);
		private bool IsRootNode => ParentNode == null;
		private int direction = 0;

		public Node(
			IModelViewService modelViewService,
			INodeService nodeService,
			ILinkService linkService,
			Node parent,
			NodeName name,
			NodeType type)
		{
			this.modelViewService = modelViewService;
			this.nodeService = nodeService;
			ParentNode = parent;
			NodeName = name;
			NodeType = type;
			PersistentNodeColor = null;
			Links = new NodeLinks(linkService);
			RootNode = parent?.RootNode ?? this;
			IsEqualWhen(NodeName);
		}


		public NodeName NodeName { get; }
		public NodeType NodeType { get; private set; }

		public Rect nodeBounds;
		public Rect NodeBounds
		{
			get
			{
				return NodeType == NodeType.MemberType
			? new Rect(nodeBounds.X, nodeBounds.Y,
					nodeBounds.Width.MM(nodeBounds.Width, 150 / NodeScale),
					nodeBounds.Height.MM(nodeBounds.Height, 40 / NodeScale))
					: nodeBounds;
			}
			set { nodeBounds = value; }
		}

		public double NodeScale => ParentNode?.ItemsScale ?? 1.0;

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

		public Point GetChildToParentCanvasPoint(Point childPoint) =>
			itemsCanvas.GetChildToParentCanvasPoint(childPoint);

		private static bool IsVisibleAtScale(double scale) => scale > 0.15;

		public string DebugToolTip => ItemsToolTip;


		public string ItemsToolTip =>
			$"\nChildren: {ChildNodes.Count}, Lines: {Links.OwnedLines.Count}\n" +
			$"Total Nodes: {CountShowingNodes()}, Lines: {CountShowingLines()}\n" +
			$"Node Scale: {NodeScale:0.00}, Items Scale: {ItemsScale:0.00}, (Factor: {ItemsScaleFactor:0.00})\n";

		public string ZoomToolTip =>
			$"\n Children: {ChildNodes.Count} Shown Items: {CountShowingNodes()}\n" +
			$"Items Scale: {ItemsScale:0.00}, Scalefactor: {ItemsScaleFactor:0.00}\n" +
			$"Offset: {ItemsOffset.TS()}, CanvasOffset: {ItemsCanvasOffset.TS()}\n" +
			$"Rect: {NodeBounds.TS()}\n" +
			$"Pos in parent coord: {ParentNode?.itemsCanvas?.GetChildToParentCanvasPoint(NodeBounds.Location).TS()}\n" +
			$"Pos in child coord: {ParentNode?.itemsCanvas?.GetParentToChildCanvasPoint(ParentNode?.itemsCanvas?.GetChildToParentCanvasPoint(NodeBounds.Location) ?? new Point(0, 0)).TS()}\n" +
			$"Pos in mainwindow coord: {itemsCanvas?.GetDevicePoint().TS()}\n" +
			$"Visual area {itemsCanvas?.ViewArea.TS()}\n" +
			$"Recursive viewArea {itemsCanvas?.GetVisualAncestorsArea().TS()}\n\n" +
			$"Parent {ParentNode?.NodeName}:{ParentNode?.DebugToolTip}";


		public Node RootNode { get; }


		public bool CanShowNode() => IsVisibleAtScale(NodeScale);



		public void Show(ItemsCanvas rootItemsCanvas)
		{
			Asserter.Requires(IsRootNode);

			InitNodeTree(rootItemsCanvas);

			UpdateNodeVisibility();
		}


		public void Clear()
		{
			if (ChildNodes.Any())
			{
				ChildNodes.ForEach(child => child.Clear());
				itemsCanvas?.Clear();
			}
		}


		public void Zoom(double zoomFactor, Point? zoomCenter = null)
		{
			if (IsMinZoomLimit(zoomFactor))
			{
				return;
			}

			itemsCanvas.Zoom(zoomFactor, zoomCenter);

			UpdateNodeVisibility();

			Links.OwnedLines
				.Where(line => line.Source == this || line.Target == this)
				.ForEach(line => line.UpdateVisibility());
		}



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
				if (direction > 0 || (direction == 0 && size.Width * NodeScale > 60 && size.Height * NodeScale > 30))
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
			}

			if (size.Width * NodeScale < 40 || size.Height * NodeScale < 20)
			{
				return;
			}

			NodeBounds = new Rect(newLocation, size);

			ParentNode.itemsCanvas.UpdateItem(viewModel);

			Links.ReferencingLines.ForEach(line => line.UpdateVisibility());

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

			Links.ReferencingLines.ForEach(line => line.UpdateVisibility());

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

			Links.OwnedLines
				.Where(line => line.Source == this || line.Target == this)
				.Concat(Links.ReferencingLines.Where(line => line.Source != this && line.Target != this))
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

			//Links.OwnedLines
			//	.Where(line => line.Target == this)
			//	.ForEach(line => line.UpdateVisibility());
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
				nodeBrush = nodeService.GetBrushFromHex(PersistentNodeColor);
			}
			else
			{
				nodeBrush = nodeService.GetRandomRectangleBrush();
				PersistentNodeColor = nodeService.GetHexColorFromBrush(nodeBrush);
			}

			return nodeBrush;
		}


		public Brush GetBackgroundNodeBrush()
		{
			Brush brush = GetNodeBrush();
			return nodeService.GetRectangleBackgroundBrush(brush);
		}


		public Brush GetHighlightNodeBrush()
		{
			Brush brush = GetNodeBrush();
			return nodeService.GetRectangleHighlightBrush(brush);
		}



		public void UpdateItem(ItemViewModel itemViewModel)
		{
			itemsCanvas.UpdateItem(itemViewModel);
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



		public void UpdateNodeVisibility()
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

				var linesToShow = Links.OwnedLines
					.Where(line => !line.ViewModel.CanShow && line.CanShowSegment())
					.Select(line => line.ViewModel);

				var linesToHide = Links.OwnedLines
					.Where(line => line.ViewModel.CanShow && !line.CanShowSegment())
					.Select(line => line.ViewModel);


				var itemsToShow = childrenToShow.Concat(linesToShow);
				var itemsToHide = childrenToHide.Concat(linesToHide);

				itemsToShow.ForEach(item => item.Show());
				itemsToHide.ForEach(item => item.Hide());

				var itemsToUpdate = itemsToHide.Concat(itemsToShow).ToList();

				itemsCanvas.UpdateItems(itemsToUpdate);
			}

			if (IsShowing)
			{
				viewModel.NotifyAll();

				childrenToUpdate.ForEach(child => child.UpdateNodeVisibility());
				Links.OwnedLines.ForEach(line => line.UpdateVisibility());
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
			Stack<Node> nodes = new Stack<Node>();

			nodes.Push(RootNode);

			int count = 0;
			while (nodes.Any())
			{
				Node node = nodes.Pop();
				if (node.IsShowing)
				{
					count++;
					node.ChildNodes.ForEach(nodes.Push);
				}
			}

			// Skip root node, which is not shown
			return count - 1;
		}


		private int CountShowingLines()
		{
			Stack<Node> nodes = new Stack<Node>();
			nodes.Push(RootNode);

			int count = 0;
			while (nodes.Any())
			{
				Node node = nodes.Pop();
				count += node.Links.OwnedLines.Count(l => l.ViewModel.IsShowing);

				node.ChildNodes.ForEach(nodes.Push);
			}

			return count;
		}


		private void InitNodeTree(ItemsCanvas rootCanvas)
		{
			itemsCanvas = rootCanvas;
			viewModel = new CompositeNodeViewModel(modelViewService, this, rootCanvas);

			InitNode();
		}


		public void AddOwnedLineItem(LinkLine line)
		{
			itemsCanvas?.AddItem(line.ViewModel);
		}


		public void RemoveOwnedLineItem(LinkLine line)
		{
			itemsCanvas?.RemoveItem(line.ViewModel);
		}


		private void InitNode()
		{
			if (ChildNodes.Any())
			{
				nodeService.SetChildrenLayout(this);

				var childViewModels = ChildNodes.Select(childNode => childNode.CreateViewModel());
				var lineViewModels = Links.OwnedLines.Select(segment => segment.ViewModel);

				var itemViewModels = lineViewModels.Concat(childViewModels);
				itemsCanvas.AddItems(itemViewModels);

				ChildNodes.ForEach(childNode => childNode.InitNode());
			}
		}


		private ItemViewModel CreateViewModel()
		{
			if (NodeType != NodeType.MemberType)
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

				viewModel = new CompositeNodeViewModel(modelViewService, this, itemsCanvas);
			}
			else
			{
				viewModel = new MemberNodeViewModel(this);
			}

			return viewModel;
		}


		private bool IsMinZoomLimit(double zoomFactor)
		{
			double newScale = itemsCanvas.Scale * zoomFactor;

			return zoomFactor < 1 && !IsVisibleAtScale(newScale);
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