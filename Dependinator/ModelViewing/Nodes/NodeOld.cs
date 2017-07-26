using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Media;
using Dependinator.Modeling;
using Dependinator.ModelViewing.Links;
using Dependinator.ModelViewing.Private.Items;
using Dependinator.Utils;


namespace Dependinator.ModelViewing.Nodes
{
	internal class NodeOld : Equatable<NodeOld>, IItemsCanvasBounds
	{
		private const int InitialScaleFactor = 7;
		private readonly INodeService nodeService;

		private readonly List<NodeOld> childNodes = new List<NodeOld>();

		private IItemsCanvas itemsCanvas;

		private Brush nodeBrush;

		private ItemViewModel viewModel = null;

		private bool IsShowing => IsRootNode || (viewModel?.IsShowing ?? false);
		private bool IsRootNode => ParentNode == null;
		private int direction = 0;

		public NodeOld(
			INodeService nodeService,
			ILinkService linkService,
			NodeOld parent,
			NodeName name,
			NodeType type)
		{
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
		public Rect ItemBounds
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

		public bool CanShow { get; }

		public double NodeScale => ParentNode?.ItemsScale ?? 1.0;

		public NodeOld ParentNode { get; }

		public IReadOnlyList<NodeOld> ChildNodes => childNodes;
		public double ItemsScale => itemsCanvas?.Scale ?? 1;
		public double ItemsScaleFactor => itemsCanvas?.ScaleFactor ?? 1;
		public Point ItemsOffset => itemsCanvas?.Offset ?? new Point();
		public Point ItemsCanvasOffset => (Point)((Vector)ItemsOffset / ItemsScale);

		public NodeLinks Links { get; }

		public string PersistentNodeColor { get; set; }
		public Rect? PersistentNodeBounds { get; set; }
		public double? PersistentScale { get; set; }
		public Point? PersistentOffset { get; set; }

		public Point ChildCanvasPointToParentCanvasPoint(Point point) =>
			itemsCanvas.ChildToParentCanvasPoint(point);

		public Point ParentCanvasPointToChildCanvasPoint(Point point) =>
			itemsCanvas.ParentToChildCanvasPoint(point);


		private static bool IsVisibleAtScale(double scale) => scale > 0.15;




		public NodeOld RootNode { get; }

		bool IItemsCanvasBounds.IsShowing => throw new NotImplementedException();

		public bool CanShowNode() => IsVisibleAtScale(NodeScale);



		public void Show(IItemsCanvas rootItemsCanvas)
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
				itemsCanvas?.RemoveAll();
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

			Point newLocation = ItemBounds.Location + scaledOffset;
			Size size = ItemBounds.Size;

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
						newLocation = new Point(ItemBounds.Location.X + scaledOffset.X, ItemBounds.Location.Y);
						size = new Size(size.Width - scaledOffset.X, size.Height);
						direction = 1;
						isMove = true;
						move = new Vector(viewOffset.X, 0);
					}
					else if (Math.Abs(p.X - ItemBounds.Width) < dist || direction == 2)
					{
						newLocation = ItemBounds.Location;
						size = new Size(size.Width + scaledOffset.X, size.Height);
						direction = 2;
					}
					else if (Math.Abs(p.Y - 0) < dist2 || direction == 3)
					{
						newLocation = new Point(ItemBounds.Location.X, ItemBounds.Location.Y + scaledOffset.Y);
						size = new Size(size.Width, size.Height - scaledOffset.Y);
						direction = 3;
						isMove = true;
						move = new Vector(0, viewOffset.Y);
					}
					else if (Math.Abs(p.Y - ItemBounds.Height) < dist || direction == 4)
					{
						newLocation = ItemBounds.Location;
						size = new Size(size.Width, size.Height + scaledOffset.Y);
						direction = 4;
					}
				}
			}

			if (size.Width * NodeScale < 40 || size.Height * NodeScale < 20)
			{
				return;
			}

			ItemBounds = new Rect(newLocation, size);

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

			Point newLocation = ItemBounds.Location;
			Size size = ItemBounds.Size;

			if (viewPosition2.HasValue)
			{
				Point p = new Point(viewPosition2.Value.X / NodeScale, viewPosition2.Value.Y / NodeScale);
				double dist = 5 / NodeScale;

				if (Math.Abs(p.X - 0) < dist)
				{
					newLocation = new Point(ItemBounds.Location.X + scaledOffset.X, ItemBounds.Location.Y);
					size = new Size(size.Width - scaledOffset.X, size.Height);
				}
				else if (Math.Abs(p.X - ItemBounds.Width) < dist)
				{
					newLocation = ItemBounds.Location;
					size = new Size(size.Width + scaledOffset.X, size.Height);
				}

				if (Math.Abs(p.Y - 0) < dist)
				{
					newLocation = new Point(ItemBounds.Location.X, ItemBounds.Location.Y + scaledOffset.Y);
					size = new Size(size.Width, size.Height - scaledOffset.Y);
				}
				else if (Math.Abs(p.Y - ItemBounds.Height) < dist)
				{
					newLocation = ItemBounds.Location;
					size = new Size(size.Width, size.Height + scaledOffset.Y);
				}
			}

			if (size.Width * NodeScale < 40 || size.Height * NodeScale < 20)
			{
				return;
			}

			ItemBounds = new Rect(newLocation, size);

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

			double width = ItemBounds.Size.Width + (2 * scaledDelta);
			double height = ItemBounds.Size.Height + (2 * scaledDelta);

			if (width < 40 || height < 20)
			{
				return;
			}

			double zoomFactor = width / ItemBounds.Size.Width;
			Point newLocation = new Point(ItemBounds.X - scaledDelta, ItemBounds.Y);
			Size newItemSize = new Size(width, height);
			ItemBounds = new Rect(newLocation, newItemSize);

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
			Stack<NodeOld> nodes = new Stack<NodeOld>();
			nodes.Push(this);

			while (nodes.Any())
			{
				NodeOld node = nodes.Pop();

				if (node.ChildNodes.Any())
				{
					//node.itemsCanvas.UpdateScale();
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


		public void AddChild(NodeOld child)
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
			IEnumerable<NodeOld> childrenToUpdate = Enumerable.Empty<NodeOld>();

			if (ChildNodes.Any())
			{
				//itemsCanvas.UpdateScale();

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

				//itemsToShow.ForEach(item => item.Show());
				//itemsToHide.ForEach(item => item.Hide());

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
			foreach (NodeOld childNode in ChildNodes)
			{
				if (childNode.viewModel?.CanShow ?? false)
				{
					childNode.HideAllChildren();
					//	childNode.viewModel.Hide();
				}
			}

			UpdateShownItems();
		}


		private void InitNodeTree(IItemsCanvas rootCanvas)
		{
			itemsCanvas = rootCanvas;
			//	viewModel = new NamespaceViewModel(itemsService, this, rootCanvas);

			InitNode();
		}


		public void AddOwnedLineItem(LinkLineOld line)
		{
			itemsCanvas?.AddItem(line.ViewModel);
		}


		public void RemoveOwnedLineItem(LinkLineOld line)
		{
			itemsCanvas?.RemoveItem(line.ViewModel);
		}


		private void InitNode()
		{
			if (ChildNodes.Any())
			{
				//nodeService.SetChildrenLayout(this);

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
				itemsCanvas = ParentNode.itemsCanvas.CreateChild(this);

				//double scale = PersistentScale ?? 0;
				//if (Math.Abs(scale) > 0.001)
				//{
				//	itemsCanvas.Scale = scale;
				//}
				//else
				//{
				//	itemsCanvas.Scale = ParentNode.itemsCanvas.Scale / InitialScaleFactor;
				//}

				//Point offset = PersistentOffset ?? new Point(0, 0);
				//itemsCanvas.Offset = offset;

				//if (NodeType == NodeType.TypeType)
				//{
				//	viewModel = new TypeViewModel(itemsService, this, itemsCanvas);
				//}
				//else
				//{
				//	viewModel = new NamespaceViewModel(itemsService, this, itemsCanvas);
				//}
			}
			else
			{
				//viewModel = new MemberNodeViewModel(this);
			}

			return viewModel;
		}


		private bool IsMinZoomLimit(double zoomFactor)
		{
			double newScale = itemsCanvas.Scale * zoomFactor;

			return zoomFactor < 1 && !IsVisibleAtScale(newScale);
		}

		public IEnumerable<NodeOld> Ancestors()
		{
			NodeOld current = ParentNode;

			while (current != null)
			{
				yield return current;
				current = current.ParentNode;
			}
		}


		public IEnumerable<NodeOld> AncestorsAndSelf()
		{
			yield return this;

			foreach (NodeOld ancestor in Ancestors())
			{
				yield return ancestor;
			}
		}

		public IEnumerable<NodeOld> Descendents()
		{
			foreach (NodeOld child in ChildNodes)
			{
				yield return child;

				foreach (NodeOld descendent in child.Descendents())
				{
					yield return descendent;
				}
			}
		}

		public IEnumerable<NodeOld> DescendentsAndSelf()
		{
			yield return this;

			foreach (NodeOld descendent in Descendents())
			{
				yield return descendent;
			}
		}
	}
}