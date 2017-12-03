using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using Dependinator.ModelHandling.Core;
using Dependinator.ModelHandling.Private.Items;
using Dependinator.ModelViewing.Links;
using Dependinator.Utils;
using Dependinator.Utils.UI;
using Dependinator.Utils.UI.Mvvm;


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


		public override bool CanShow => !Node.IsHidden
			&& (ItemScale * ItemWidth > 20 && Node.Parent.CanShowChildren);

		public bool CanShowChildren => ItemScale * ItemWidth > 50 * 7;

		public Node Node { get; }

		public bool IsHorizontal => !IsVertical;
		public bool IsVertical => (ItemWidth * 1.5 < ItemHeight) && ItemWidth * ItemScale < 80;

		public Brush RectangleBrush { get; }
		public Brush TitleBorderBrush => Node.NodeType == NodeType.Type ? RectangleBrush : null;
		public Brush BackgroundBrush { get; }

		public bool IsShowNode => ItemScale < 100;

		public bool IsShowItems => CanShowChildren;

		public bool IsShowDescription => !string.IsNullOrEmpty(Description) && !CanShowChildren;

		public string Name => Node.Name.DisplayName;

		public double RectangleLineWidth => IsShowPoints ? 0.6 * 3 : IsMouseOver ? 0.6 * 1.5 : 0.6;

		public string ToolTip { get => Get(); set => Set(value); }

		public void UpdateToolTip() => ToolTip =
			$"{Node.Name.DisplayFullNameWithType}" +
			$"\n{Node.Description}" +
			$"\nLines; In: {IncomingLinesCount}, Out: {OutgoingLinesCount}" +
			$"\nLinks; In: {IncomingLinksCount}, Out: {OutgoingLinksCount}" +
			$"{DebugToolTip}";

		public int IncomingLinesCount => Node.TargetLines.Count(line => line.Owner != Node);

		public Command HideNodeCommand => Command(HideNode);

		
		public int IncomingLinksCount => Node.TargetLines
			.Where(line => line.Owner != Node)
			.SelectMany(line => line.Links)
			.Count();

		public int OutgoingLinesCount => Node.SourceLines.Count(line => line.Owner != Node);


		public int OutgoingLinksCount => Node.SourceLines
			.Where(line => line.Owner != Node)
			.SelectMany(line => line.Links)
			.Count();

		public int FontSize
		{
			get
			{
				int f = ((int)(10 * ItemScale)).MM(9, 13);
				//if (f == 10)
				//{
				//	// Some recomend skipping fontsize 10
				//	f = 9;
				//}

				return f;
			}
		}

		public int DescriptionFontSize => ((int)(10 * ItemScale)).MM(9, 11);
		public string Description => Node.Description;

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



		private void HideNode()
		{
			Node.IsHidden = true;
			Node.Parent.ItemsCanvas.UpdateAndNotifyAll();
		}


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


		public void OnMouseEnter(bool isTitle)
		{
			mouseOverDelay.Delay(MouseEnterDelay, _ =>
			{
				if (ModelViewModel.IsControlling)
				{
					Mouse.OverrideCursor = Cursors.Hand;
					if (!isTitle)
					{
						Point screenPoint = Mouse.GetPosition(Application.Current.MainWindow);
						Point point = ItemOwnerCanvas.RootScreenToCanvasPoint(screenPoint);
						nodeViewModelService.GetPointIndex(Node, point);
					}
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


		public void MouseMove(Point screenPoint, bool isTitle)
		{
			Point point = ItemOwnerCanvas.RootScreenToCanvasPoint(screenPoint);

			if (currentPointIndex == -1)
			{
				// First move event, lets start a move by  getting the index of point to move.
				// THis might create a new point if there is no existing point near the mouse down point
				currentPointIndex = isTitle ? 0 :
					nodeViewModelService.GetPointIndex(Node, mouseDownPoint);
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

			if (nodeViewModelService.MovePoint(Node, currentPointIndex, point, mouseMovedPoint))
			{
				mouseMovedPoint = point;
			}

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
			IEnumerable<LinkItem> items = nodeViewModelService.GetIncomingLinkItems(Node);
			return new ObservableCollection<LinkItem>(items);
		}



		private ObservableCollection<LinkItem> GetOutgoingLinkItems()
		{
			IEnumerable<LinkItem> items = nodeViewModelService.GetOutgoingLinkItems(Node);
			return new ObservableCollection<LinkItem>(items);
		}



		private string DebugToolTip => ItemsToolTip;

		private string ItemsToolTip => !Config.IsDebug ? "" :
			"\n" +
			$"Rect: {ItemBounds.TS()}\n" +
			$"Scale {ItemScale.TS()}, ChildrenScale: {Node.ItemsCanvas?.Scale.TS()}\n" +
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