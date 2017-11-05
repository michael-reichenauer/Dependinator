using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using Dependinator.ModelViewing.Links;
using Dependinator.ModelViewing.Private.Items;
using Dependinator.Utils.UI;


namespace Dependinator.ModelViewing.Nodes
{
	internal abstract class NodeViewModel : ItemViewModel
	{
		private readonly INodeViewModelService nodeViewModelService;

		private readonly DelayDispatcher mouseOverDelay = new DelayDispatcher();

		private readonly Lazy<ObservableCollection<LinkItem>> incomingLinks;
		private readonly Lazy<ObservableCollection<LinkItem>> outgoingLinks;


		private bool isFirstShow = true;
		private int currentPointIndex = -1;
		private Point mouseDownPoint;
		private Point mouseMovedPoint;
		

		protected NodeViewModel(INodeViewModelService nodeViewModelService, Node node)
		{
			Priority = 1;
			this.nodeViewModelService = nodeViewModelService;
			this.Node = node;

			RectangleBrush = nodeViewModelService.GetNodeBrush(node);
			BackgroundBrush = nodeViewModelService.GetBackgroundBrush(RectangleBrush);

			incomingLinks = new Lazy<ObservableCollection<LinkItem>>(GetIncomingLinkItems);
			outgoingLinks = new Lazy<ObservableCollection<LinkItem>>(GetOutgoingLinkItems);
		}


		public override bool CanShow => ItemScale > 0.20;

		public Node Node { get; }

		public Brush RectangleBrush { get; }
		public Brush TitleBorderBrush => Node.NodeType == NodeType.Type ? RectangleBrush : null;
		public Brush BackgroundBrush { get; }

		public bool IsShowNode => ItemScale < 100;

		public string Name => Node.Name.DisplayName;

		public double RectangleLineWidth => IsShowPoints ? 0.6 * 3 : IsMouseOver ? 0.6 * 1.5 : 0.6;

		public string ToolTip { get => Get(); set => Set(value); }

		public void UpdateToolTip() => ToolTip =
			$"{Node.Name.DisplayFullNameWithType}"+
			$"\nLinks: Incoming: {IncomingLinksCount}, Outgoing: {OutgoingLinksCount}" + 
			$"{DebugToolTip}";


		public int IncomingLinksCount => Node.TargetLines
			.Where(line => line.Owner != Node)
			.SelectMany(line => line.Links)
			.Count();

		public int OutgoingLinksCount => Node.SourceLines
			.Where(line => line.Owner != Node)
			.SelectMany(line => line.Links)
			.Count();

		public int FontSize => ((int)(15 * ItemScale)).MM(9, 13);

		public bool IsMouseOver
		{
			get => Get();
			private set => Set(value).Notify(nameof(RectangleLineWidth));
		}

		public bool IsShowPoints { get => Get(); private set => Set(value).Notify(nameof(IsShowToolTip)); }
		public bool IsShowToolTip => !IsShowPoints;

		public ItemsViewModel ItemsViewModel { get; set; }

		public ObservableCollection<LinkItem> IncomingLinks => incomingLinks.Value;

		public ObservableCollection<LinkItem> OutgoingLinks => outgoingLinks.Value;


		public override void ItemRealized()
		{
			base.ItemRealized();

			// If this node has an items canvas, make sure it knows it has been realized (fix zoom level)
			ItemsViewModel?.ItemRealized();

			if (isFirstShow)
			{
				isFirstShow = false;

				nodeViewModelService.FirstShowNode(Node);
			}
		}


		public override void ItemVirtualized()
		{
			base.ItemVirtualized();
			ItemsViewModel?.ItemVirtualized();
		}



		public string Color => RectangleBrush.AsString();

		//$"Items: {ItemOwnerCanvas.CanvasRoot.AllItemsCount()}, Shown {ItemOwnerCanvas.CanvasRoot.ShownItemsCount()}";


		public void OnMouseEnter()
		{
			mouseOverDelay.Delay(MouseEnterDelay, _ =>
			{
				if (ModelViewModel.IsControlling)
				{
					Mouse.OverrideCursor = Cursors.Hand;
				}

				if (IsResizable())
				{
					IsShowPoints = ModelViewModel.IsControlling;
				}

				IsMouseOver = true;
			});
		}


		private static TimeSpan MouseEnterDelay => ModelViewModel.IsControlling
			? TimeSpan.FromMilliseconds(1) : ModelViewModel.MouseEnterDelay;


		public void OnMouseLeave()
		{
			Mouse.OverrideCursor = null;
			mouseOverDelay.Cancel();
			IsShowPoints = false;
			IsMouseOver = false;
		}


		public void MouseDown(Point screenPoint)
		{
			mouseDownPoint = ItemOwnerCanvas.RootScreenToCanvasPoint(screenPoint);
			mouseMovedPoint = mouseDownPoint;
			currentPointIndex = -1;
		}


		public void MouseMove(Point screenPoint)
		{
			Point point = ItemOwnerCanvas.RootScreenToCanvasPoint(screenPoint);

			if (currentPointIndex == -1)
			{
				// First move event, lets start a move by  getting the index of point to move.
				// THis might create a new point if there is no existing point near the mouse down point
				currentPointIndex = nodeViewModelService.GetPointIndex(Node, mouseDownPoint);
				if (currentPointIndex == -1)
				{
					// Point not close enough to the line
					return;
				}

				if (!IsResizable())
				{
					// Only support move if scale is small
					currentPointIndex = 0;
				}
			}

			nodeViewModelService.MovePoint(Node, currentPointIndex, point, mouseMovedPoint);
			mouseMovedPoint = point;
			IsMouseOver = true;
			if (IsResizable())
			{
				IsShowPoints = true;
			}

			//Node.ItemsCanvas?.UpdateAndNotifyAll();

			//NotifyAll();
		}


		private bool IsResizable()
		{
			return ItemWidth * ItemScale > 70 || ItemHeight * ItemScale > 70;
		}


		public void MouseUp(Point screenPoint) => currentPointIndex = -1;

		public override string ToString() => Node.Name.ToString();


		private ObservableCollection<LinkItem> GetIncomingLinkItems()
		{
			IEnumerable<Link> links = Node.TargetLines
				.Where(line => line.Owner != Node)
				.SelectMany(line => line.Links)
				.DistinctBy(link => link.Source);

			IEnumerable<LinkItem> items = nodeViewModelService.GetLinkItems(links, link => link.Source, 1);
			return new ObservableCollection<LinkItem>(items);
		}

		private ObservableCollection<LinkItem> GetOutgoingLinkItems()
		{
			IEnumerable<Link> links = Node.SourceLines
				.Where(line => line.Owner != Node)
				.SelectMany(line => line.Links)
				.DistinctBy(link => link.Target);

			IEnumerable<LinkItem> items = nodeViewModelService.GetLinkItems(links, link => link.Target, 1);
			return new ObservableCollection<LinkItem>(items);
		}


		private string DebugToolTip => ItemsToolTip;

		private string ItemsToolTip =>
			"\n" +
			$"Rect: {ItemBounds.TS()}\n" +
			$"Scale {ItemScale}, ChildrenScale: {Node.ItemsCanvas?.Scale}\n" +
			$"Root Scale {Node.Root.ItemsCanvas.Scale}\n" +
			$"Level {Node.Ancestors().Count()}\n" +
			$"Items: {Node.ItemsCanvas?.ShownChildItemsCount()} ({Node.ItemsCanvas?.ChildItemsCount()})\n" +
			$"ShownDescendantsItems {Node.ItemsCanvas?.ShownDescendantsItemsCount()} ({Node.ItemsCanvas?.DescendantsItemsCount()})\n" +
			$"ParentItems {Node.Parent.ItemsCanvas.ShownChildItemsCount()} ({Node.Parent.ItemsCanvas.ChildItemsCount()})\n" +
			$"RootShownDescendantsItems {Node.Root.ItemsCanvas.ShownDescendantsItemsCount()} ({Node.Root.ItemsCanvas.DescendantsItemsCount()})\n" +
			$"Nodes: {NodesCount}, Namespaces: {NamespacesCount}, Types: {TypesCount}, Members: {MembersCount}\n" +
			$"Links: {LinksCount}, Lines: {LinesCount}";

		private int NodesCount => Node.Root.Descendents().Count();
		private int TypesCount => Node.Root.Descendents().Count(node => node.NodeType == NodeType.Type);
		private int NamespacesCount => Node.Root.Descendents().Count(node => node.NodeType == NodeType.NameSpace);
		private int MembersCount => Node.Root.Descendents().Count(node => node.NodeType == NodeType.Member);
		private int LinksCount => Node.Root.Descendents().SelectMany(node => node.SourceLinks).Count();
		private int LinesCount => Node.Root.Descendents().SelectMany(node => node.SourceLines).Count();

	}
}