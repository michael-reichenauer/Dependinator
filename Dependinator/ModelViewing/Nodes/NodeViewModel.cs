using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using Dependinator.ModelViewing.Items;
using Dependinator.ModelViewing.Lines.Private;
using Dependinator.ModelViewing.ModelHandling.Core;
using Dependinator.Utils;
using Dependinator.Utils.UI;
using Dependinator.Utils.UI.Mvvm;


namespace Dependinator.ModelViewing.Nodes
{
	internal abstract class NodeViewModel : ItemViewModel, ISelectableItem
	{
		private readonly INodeViewModelService nodeViewModelService;

		private readonly Lazy<ObservableCollection<LineMenuItemViewModel>> incomingLinks;
		private readonly Lazy<ObservableCollection<LineMenuItemViewModel>> outgoingLinks;
		public static readonly TimeSpan MouseEnterDelay = TimeSpan.FromMilliseconds(300);
		private readonly DelayDispatcher delayDispatcher = new DelayDispatcher();

		private bool isFirstShow = true;
		private readonly Brush backgroundBrush;
		private readonly Brush selectedBrush;


		protected NodeViewModel(
			INodeViewModelService nodeViewModelService,
			Node node)
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
		public bool IsShowCodeButton { get => Get(); set => Set(value); }
		public bool IsShowNode => ItemScale < 100;

		public bool IsShowItems => CanShowChildren;

		public bool IsShowDescription => (!CanShowChildren || !Node.Children.Any());
		public bool IsShowToolTip => true;
		public bool HasCode => Node.CodeText != null;
		public Command ShowCodeCommand => Command(() => ShowCode());


		public void ShowCode() => nodeViewModelService.ShowCode(Node);


		public string Name => Node.Name.DisplayName;

		private NodeControlViewModel view2Model;


		public bool IsInnerSelected { get => Get(); set => Set(value); }


		public double RectangleLineWidth => 1;

		public string ToolTip { get => Get(); set => Set(value); }
		public int IncomingLinesCount => Node.TargetLines.Count(line => line.Owner != Node);

		public Command HideNodeCommand => Command(HideNode);
		public Command ShowDependenciesCommand => Command(ShowDependencies);


		public int FontSize => ((int)(10 * ItemScale)).MM(9, 13);

		public int CodeIconSize => ((int)(15 * ItemScale)).MM(15, 60);

		public int DescriptionFontSize => ((int)(10 * ItemScale)).MM(9, 11);
		public string Description => Node.Description;
		public bool IsShowCodeIcon => Node.CodeText != null && ItemScale > 1.3;


		public void ShowDependencies() => nodeViewModelService.ShowReferences(this);




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


		public void UpdateToolTip() => ToolTip =
			$"{Node.Name.DisplayFullName}" +
			(string.IsNullOrWhiteSpace(Node.Description) ? "" : $"\n{Node.Description}") +
			$"{DebugToolTip}";


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


		public ItemsViewModel ItemsViewModel { get; set; }

		public ObservableCollection<LineMenuItemViewModel> IncomingLinks => incomingLinks.Value;

		public ObservableCollection<LineMenuItemViewModel> OutgoingLinks => outgoingLinks.Value;



		public void HideNode()
		{
			if (!Node.View.IsHidden)
			{
				HideNode(Node);
				
				Node.Parent.View.ItemsCanvas.UpdateAndNotifyAll();
				Node.Root.View.ItemsCanvas.UpdateAll();
			}
		}

		private void HideNode(Node node)
		{
			node.DescendentsAndSelf().ForEach(n => n.View.IsHidden = true);
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


		public void RearrangeLayout() => nodeViewModelService.RearrangeLayout(this);


		public string Color => RectangleBrush.AsString();


		public void MouseEnterTitle()
		{
			delayDispatcher.Delay(MouseEnterDelay, _ => { IsShowCodeButton = Node.CodeText != null; });
		}


		public void MouseExitTitle()
		{
			delayDispatcher.Cancel();
			IsShowCodeButton = false;
		}


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



		private string DebugToolTip => "" + ItemsToolTip;


		private string ItemsToolTip => !BuildConfig.IsDebug ? "" :
			"\n" +
			$"\nLines; In: {IncomingLinesCount}, Out: {OutgoingLinesCount}" +
			$"\nLinks; In: {IncomingLinksCount}, Out: {OutgoingLinksCount}\n" +
			$"Rect: {ItemBounds.TS()}\n" +
			$"Scale {ItemScale.TS()}, ChildrenScale: {Node.View.ItemsCanvas?.Scale.TS()}\n" +
			$"ScaleFactor: {Node.View.ScaleFactor}, {Node.View.ViewModel.ItemsViewModel.ItemsCanvas.ScaleFactor}\n" +
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