using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using Dependinator.ModelViewing.Items;
using Dependinator.ModelViewing.Items.Private;
using Dependinator.ModelViewing.Links.Private;
using Dependinator.ModelViewing.ModelHandling.Core;
using Dependinator.ModelViewing.Nodes.Private;
using Dependinator.Utils;
using Dependinator.Utils.UI.Mvvm;


namespace Dependinator.ModelViewing.Nodes
{
	internal abstract class NodeViewModel : ItemViewModel, ISelectableItem
	{
		private readonly INodeViewModelService nodeViewModelService;

		private readonly Lazy<ObservableCollection<LineMenuItemViewModel>> incomingLinks;
		private readonly Lazy<ObservableCollection<LineMenuItemViewModel>> outgoingLinks;

		private bool isFirstShow = true;
		private readonly Brush backgroundBrush;
		private readonly Brush selectedBrush;


		protected NodeViewModel(INodeViewModelService nodeViewModelService, Node node)
		{
			Priority = 1;
			this.nodeViewModelService = nodeViewModelService;
			this.Node = node;

			RectangleBrush = nodeViewModelService.GetNodeBrush(node);
			backgroundBrush = nodeViewModelService.GetBackgroundBrush(RectangleBrush);
			selectedBrush = nodeViewModelService.GetSelectedBrush(RectangleBrush);


			incomingLinks = new Lazy<ObservableCollection<LineMenuItemViewModel>>(GetIncomingLinkItems);
			outgoingLinks = new Lazy<ObservableCollection<LineMenuItemViewModel>>(GetOutgoingLinkItems);
		}


		public override bool CanShow => !Node.View.IsHidden
			&& (ItemScale * ItemWidth > 20 && Node.Parent.View.CanShowChildren);

		public bool CanShowChildren => ItemScale * ItemWidth > 50 * 7;

		public Node Node { get; }

		public bool IsHorizontal => !IsVertical;
		public bool IsVertical => (ItemWidth * 1.5 < ItemHeight) && ItemWidth * ItemScale < 80;

		public Brush RectangleBrush { get; }
		public Brush TitleBorderBrush => Node.NodeType == NodeType.Type ? RectangleBrush : null;
		public Brush BackgroundBrush => IsSelected ? selectedBrush : backgroundBrush;

		public bool IsShowNode => ItemScale < 100;

		public bool IsShowItems => CanShowChildren;

		public bool IsShowDescription => !string.IsNullOrEmpty(Description) && !CanShowChildren;

		public string Name => Node.Name.DisplayName;

		private NodeControlViewModel view2Model;

		public bool IsSelected
		{
			get => view2Model != null;
			set
			{
				if (value)
				{
					view2Model = new NodeControlViewModel(this);
					Node.Parent.View.ItemsCanvas.AddItem(view2Model);
				}
				else
				{
					if (view2Model != null)
					{
						Node.Parent.View.ItemsCanvas.RemoveItem(view2Model);
						view2Model = null;
					}
				}

				Notify(nameof(BackgroundBrush));
				IsInnerSelected = false;
				if (ItemsViewModel?.ItemsCanvas != null)
				{
					ItemsViewModel.ItemsCanvas.IsFocused = false;
				}
			}
		}

		public bool IsInnerSelected { get => Get(); set => Set(value); }


		public double RectangleLineWidth => 1;

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


		public void MouseClicked(MouseButtonEventArgs e)
		{
			nodeViewModelService.MouseClicked(this);
		}


		public override void MoveItem(Vector moveOffset)
		{
			Point newLocation = ItemBounds.Location + moveOffset;
			ItemBounds = new Rect(newLocation, ItemBounds.Size);
		}


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


		public ItemsViewModel ItemsViewModel { get; set; }

		public ObservableCollection<LineMenuItemViewModel> IncomingLinks => incomingLinks.Value;

		public ObservableCollection<LineMenuItemViewModel> OutgoingLinks => outgoingLinks.Value;



		private void HideNode()
		{
			Node.View.IsHidden = true;
			Node.Parent.View.ItemsCanvas.UpdateAndNotifyAll();
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



		public override string ToString() => Node.Name.ToString();


		private ObservableCollection<LineMenuItemViewModel> GetIncomingLinkItems()
		{
			IEnumerable<LineMenuItemViewModel> items = nodeViewModelService.GetIncomingLinkItems(Node);
			return new ObservableCollection<LineMenuItemViewModel>(items);
		}



		private ObservableCollection<LineMenuItemViewModel> GetOutgoingLinkItems()
		{
			IEnumerable<LineMenuItemViewModel> items = nodeViewModelService.GetOutgoingLinkItems(Node);
			return new ObservableCollection<LineMenuItemViewModel>(items);
		}



		private string DebugToolTip => ItemsToolTip;

		private string ItemsToolTip => !Config.IsDebug ? "" :
			"\n" +
			$"Rect: {ItemBounds.TS()}\n" +
			$"Scale {ItemScale.TS()}, ChildrenScale: {Node.View.ItemsCanvas?.Scale.TS()}\n" +
			//$"C Point {Node.ItemsCanvas.CanvasPointToScreenPoint(new Point(0, 0)).TS()}\n" +
			//$"R Point {Node.ItemsCanvas.CanvasPointToScreenPoint2(new Point(0, 0)).TS()}\n" +
			//$"M Point {Mouse.GetPosition(Node.Root.ItemsCanvas.ZoomableCanvas).TS()}\n" +
			$"Root Scale {Node.Root.View.ItemsCanvas.Scale}\n" +
			$"Level {Node.Ancestors().Count()}\n" +
			$"Items: {Node.View.ItemsCanvas?.ShownChildItemsCount()} ({Node.View.ItemsCanvas?.ChildItemsCount()})\n" +
			$"ShownDescendantsItems {Node.View.ItemsCanvas?.ShownDescendantsItemsCount()} ({Node.View.ItemsCanvas?.DescendantsItemsCount()})\n" +
			$"ParentItems {Node.Parent.View.ItemsCanvas.ShownChildItemsCount()} ({Node.Parent.View.ItemsCanvas.ChildItemsCount()})\n" +
			$"RootShownDescendantsItems {Node.Root.View.ItemsCanvas.ShownDescendantsItemsCount()} ({Node.Root.View.ItemsCanvas.DescendantsItemsCount()})\n" +
			$"Nodes: {NodesCount}, Namespaces: {NamespacesCount}, Types: {TypesCount}, Members: {MembersCount}\n" +
			$"Links: {LinksCount}, Lines: {LinesCount}";

		private int NodesCount => Node.Root.Descendents().Count();
		private int TypesCount => Node.Root.Descendents().Count(node => node.NodeType == NodeType.Type);
		private int NamespacesCount => Node.Root.Descendents().Count(node => node.NodeType == NodeType.NameSpace);
		private int MembersCount => Node.Root.Descendents().Count(node => node.NodeType == NodeType.Member);
		private int LinksCount => Node.Root.Descendents().SelectMany(node => node.SourceLinks).Count();
		private int LinesCount => Node.Root.Descendents().SelectMany(node => node.SourceLines).Count();



		public void OnMouseWheel(UIElement uiElement, MouseWheelEventArgs e) =>
			nodeViewModelService.OnMouseWheel(this, uiElement, e);
	}
}