using System;
using System.Linq;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using Dependinator.ModelViewing.DataHandling.Dtos;
using Dependinator.ModelViewing.Items;
using Dependinator.ModelViewing.ModelHandling.Core;
using Dependinator.Utils;
using Dependinator.Utils.UI;
using Dependinator.Utils.UI.Mvvm;


namespace Dependinator.ModelViewing.Nodes
{
	internal abstract class NodeViewModel : ItemViewModel, ISelectableItem
	{
		private readonly INodeViewModelService nodeViewModelService;

		public static readonly TimeSpan MouseEnterDelay = TimeSpan.FromMilliseconds(300);
		private readonly DelayDispatcher delayDispatcher = new DelayDispatcher();


		private readonly Brush backgroundBrush;
		private readonly Brush selectedBrush;
		private readonly Brush rectangleBrush;
		private readonly Brush dimBrush;
		private readonly Brush titleBrush;


		protected NodeViewModel(
			INodeViewModelService nodeViewModelService,
			Node node)
		{
			Priority = 1;
			this.nodeViewModelService = nodeViewModelService;
			this.Node = node;

			rectangleBrush = nodeViewModelService.GetNodeBrush(node);
			dimBrush = nodeViewModelService.GetDimBrush();
			titleBrush = nodeViewModelService.GetTitleBrush();
			backgroundBrush = nodeViewModelService.GetBackgroundBrush(rectangleBrush);
			selectedBrush = nodeViewModelService.GetSelectedBrush(rectangleBrush);
		}

		public bool IsFirstShow { get; set; } = true;

		public override bool CanShow
		{
			get
			{
				if (Node.View.IsHidden && Node.Parent.View.IsHidden)
				{
					return false;
				}

				return (ItemScale * ItemWidth > 20 && Node.Parent.View.CanShowChildren);
			}
		}

		public bool CanShowChildren => ItemScale * ItemWidth > 50 * 7;

		public Node Node { get; }

		public bool IsHorizontal => !IsVertical;
		public bool IsVertical => (ItemWidth * 1.5 < ItemHeight) && ItemWidth * ItemScale < 80;

		public Brush TitleBrush => Node.View.IsHidden ? dimBrush : titleBrush;
		public Brush RectangleBrush => Node.View.IsHidden ? dimBrush : rectangleBrush;
		public Brush TitleBorderBrush => Node.NodeType == NodeType.Type ? RectangleBrush : null;
		public Brush BackgroundBrush => IsSelected ? selectedBrush : backgroundBrush;

		public bool IsShowCodeButton { get => Get(); set => Set(value); }
		public bool IsHidden => Node.View.IsHidden;
		public bool IsShowNode => ItemScale < 70;
		public bool IsShowItems => CanShowChildren;
		public bool IsShowDescription => (!CanShowChildren || !Node.Children.Any());
		public bool IsShowToolTip => true;
		public bool HasCode => Node.NodeType == NodeType.Type || Node.NodeType == NodeType.Member;
		public Command ShowCodeCommand => Command(() => ShowCode());


		public void ShowCode() => nodeViewModelService.ShowCode(Node);


		public string Name => Node.Name.DisplayShortName;

		private NodeControlViewModel view2Model;


		public bool IsInnerSelected { get => Get(); set => Set(value); }


		public double RectangleLineWidth => 1;

		public string ToolTip { get => Get(); set => Set(value); }

		public Command HideNodeCommand => Command(HideNode);
		public Command ShowDependenciesCommand => Command(ShowDependencies);


		public int FontSize => ((int)(10 * ItemScale)).MM(9, 13);

		public int CodeIconSize => ((int)(15 * ItemScale)).MM(15, 60);

		public int DescriptionFontSize => ((int)(10 * ItemScale)).MM(9, 11);
		public string Description => Node.Description;
		public bool IsShowCodeIcon => HasCode && ItemScale > 1.3 && !IsHidden;


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
			$"{Node.Name.DisplayLongName}" +
			(string.IsNullOrWhiteSpace(Node.Description) ? "" : $"\n{Node.Description}") +
			$"{DebugToolTip}";



		public void MouseClicked(MouseButtonEventArgs e)
		{
			nodeViewModelService.MouseClicked(this);
		}


		public override void MoveItem(Vector moveOffset)
		{
			Point newLocation = ItemBounds.Location + moveOffset;
			ItemBounds = new Rect(newLocation, ItemBounds.Size);
		}


		public ItemsViewModel ItemsViewModel { get; set; }



		public void HideNode() => nodeViewModelService.HideNode(Node);

		public void ShowNode() => nodeViewModelService.ShowNode(Node);


		public override void ItemRealized()
		{
			base.ItemRealized();

			// If this node has an items canvas, make sure it knows it has been realized (fix zoom level)
			ItemsViewModel?.ItemRealized();

			if (IsFirstShow)
			{
				IsFirstShow = false;

				nodeViewModelService.FirstShowNode(Node);
			}
		}


		public override void ItemVirtualized()
		{
			base.ItemVirtualized();
			ItemsViewModel?.ItemVirtualized();
		}


		public void RearrangeLayout() => nodeViewModelService.RearrangeLayout(this);


		public string Color => rectangleBrush.AsString();


		public void MouseEnterTitle()
		{
			delayDispatcher.Delay(MouseEnterDelay, _ => { IsShowCodeButton = HasCode; });
		}


		public void MouseExitTitle()
		{
			delayDispatcher.Cancel();
			IsShowCodeButton = false;
		}


		public override string ToString() => Node.Name.ToString();



		private string DebugToolTip => "" + ItemsToolTip;


		private string ItemsToolTip => !BuildConfig.IsDebug ? "" :
			$"\n\n" +
			$"FullName: {Node.Name.FullName}\n" +
			$"Rect: {ItemBounds.TS()}\n" +
			$"Scale {ItemScale.TS()}, ChildrenScale: {Node.View.ItemsCanvas?.Scale.TS()}\n" +
			$"ScaleFactor: {Node.View.ScaleFactor}, {Node.View.ViewModel.ItemsViewModel?.ItemsCanvas?.ScaleFactor}\n" +
			//$"C Point {Node.ItemsCanvas.CanvasPointToScreenPoint(new Point(0, 0)).TS()}\n" +
			//$"R Point {Node.ItemsCanvas.CanvasPointToScreenPoint2(new Point(0, 0)).TS()}\n" +
			//$"M Point {Mouse.GetPosition(Node.Root.ItemsCanvas.ZoomableCanvas).TS()}\n" +
			$"Root Scale {Node.Root.View.ItemsCanvas?.Scale}\n" +
			$"Level {Node.Ancestors().Count()}\n" +
			$"Items: {Node.View.ItemsCanvas?.ShownChildItemsCount()} ({Node.View.ItemsCanvas?.ChildItemsCount()})\n" +
			$"ShownDescendantsItems {Node.View.ItemsCanvas?.ShownDescendantsItemsCount()} ({Node.View.ItemsCanvas?.DescendantsItemsCount()})\n" +
			$"ParentItems {Node.Parent.View.ItemsCanvas?.ShownChildItemsCount()} ({Node.Parent.View.ItemsCanvas?.ChildItemsCount()})\n" +
			$"RootShownDescendantsItems {Node.Root.View.ItemsCanvas?.ShownDescendantsItemsCount()} ({Node.Root.View.ItemsCanvas?.DescendantsItemsCount()})\n" +
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