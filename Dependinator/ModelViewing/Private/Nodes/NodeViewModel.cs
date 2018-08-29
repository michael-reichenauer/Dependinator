using System;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using Dependinator.ModelViewing.Private.DataHandling.Dtos;
using Dependinator.ModelViewing.Private.ItemsViewing;
using Dependinator.ModelViewing.Private.ModelHandling.Core;
using Dependinator.Utils;
using Dependinator.Utils.UI;
using Dependinator.Utils.UI.Mvvm;


namespace Dependinator.ModelViewing.Private.Nodes
{
    internal abstract class NodeViewModel : ItemViewModel, ISelectableItem
    {
        public static readonly TimeSpan MouseEnterDelay = TimeSpan.FromMilliseconds(300);


        private readonly Brush backgroundBrush;
        private readonly DelayDispatcher delayDispatcher = new DelayDispatcher();
        private readonly Brush dimBrush;
        private readonly INodeViewModelService nodeViewModelService;
        private readonly Brush rectangleBrush;
        private readonly Brush selectedBrush;
        private readonly Brush titleBrush;

        private NodeControlViewModel view2Model;


        protected NodeViewModel(
            INodeViewModelService nodeViewModelService,
            Node node)
        {
            Priority = 1;
            this.nodeViewModelService = nodeViewModelService;
            Node = node;

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
                if (Node.IsHidden && Node.Parent.IsHidden)
                {
                    return false;
                }

                return ItemScale * ItemWidth > 20 && Node.Parent.CanShowChildren;
            }
        }

        public bool CanShowChildren => ItemScale * ItemWidth > 60 * 7;

        public Node Node { get; }

        public bool IsHorizontal => !IsVertical;
        public bool IsVertical => ItemWidth * 1.5 < ItemHeight && ItemWidth * ItemScale < 80;

        public Brush TitleBrush => Node.IsHidden ? dimBrush : titleBrush;
        public Brush RectangleBrush => Node.IsHidden ? dimBrush : rectangleBrush;
        public Brush TitleBorderBrush => Node.NodeType.IsType() ? RectangleBrush : null;
        public Brush BackgroundBrush => IsSelected ? selectedBrush : backgroundBrush;

        public bool IsShowCodeButton { get => Get(); set => Set(value); }
        public bool IsHidden => Node.IsHidden;
        public bool IsShowNode => ItemScale < 70;
        public bool IsShowItems => CanShowChildren;
        public bool IsShowDescription => !CanShowChildren || !Node.Children.Any();
        public bool IsShowToolTip => true;
        public bool HasCode => Node.NodeType.IsType() || Node.NodeType.IsMember();
        public Command ShowCodeCommand => AsyncCommand(() => ShowCodeAsync());


        public string Name => Node.Name.DisplayShortName;


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


        public ItemsViewModel ItemsViewModel { get; set; }


        public string Color => rectangleBrush.AsString();


        private string DebugToolTip => "" + ItemsToolTip;


        private string ItemsToolTip => !BuildConfig.IsDebug
            ? ""
            : $"\n\n" +
              $"FullName: {Node.Name.FullName}\n" +
              $"Rect: {ItemBounds.TS()}\n" +
              $"Scale {ItemScale.TS()}, ChildrenScale: {Node.ItemsCanvas?.Scale.TS()}\n" +
              $"ScaleFactor: {Node.ScaleFactor}, {Node.ViewModel.ItemsViewModel?.ItemsCanvas?.ScaleFactor}\n" +
              //$"C Point {Node.ItemsCanvas.CanvasPointToScreenPoint(new Point(0, 0)).TS()}\n" +
              //$"R Point {Node.ItemsCanvas.CanvasPointToScreenPoint2(new Point(0, 0)).TS()}\n" +
              //$"M Point {Mouse.GetPosition(Node.Root.ItemsCanvas.ZoomableCanvas).TS()}\n" +
              $"Root Scale {Node.Root.ItemsCanvas?.Scale}\n" +
              $"Level {Node.Ancestors().Count()}\n" +
              $"Items: {Node.ItemsCanvas?.ShownChildItemsCount()} ({Node.ItemsCanvas?.ChildItemsCount()})\n" +
              $"ShownDescendantsItems {Node.ItemsCanvas?.ShownDescendantsItemsCount()} ({Node.ItemsCanvas?.DescendantsItemsCount()})\n" +
              $"ParentItems {Node.Parent.ItemsCanvas?.ShownChildItemsCount()} ({Node.Parent.ItemsCanvas?.ChildItemsCount()})\n" +
              $"RootShownDescendantsItems {Node.Root.ItemsCanvas?.ShownDescendantsItemsCount()} ({Node.Root.ItemsCanvas?.DescendantsItemsCount()})\n" +
              $"Nodes: {NodesCount}, Namespaces: {NamespacesCount}, Types: {TypesCount}, Members: {MembersCount}\n" +
              $"Links: {LinksCount}, Lines: {LinesCount}";

        private int NodesCount => Node.Root.Descendents().Count();
        private int TypesCount => Node.Root.Descendents().Count(node => node.NodeType.IsType());
        private int NamespacesCount => Node.Root.Descendents().Count(node => node.NodeType.IsNamespace());
        private int MembersCount => Node.Root.Descendents().Count(node => node.NodeType.IsMember());
        private int LinksCount => Node.Root.Descendents().SelectMany(node => node.SourceLinks).Count();
        private int LinesCount => Node.Root.Descendents().SelectMany(node => node.SourceLines).Count();


        public bool IsSelected
        {
            get => view2Model != null;
            set
            {
                if (value)
                {
                    view2Model = new NodeControlViewModel(this);
                    Node.Root.ItemsCanvas.AddItem(view2Model);
                }
                else
                {
                    if (view2Model != null)
                    {
                        Node.Root.ItemsCanvas.RemoveItem(view2Model);
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


        public Task ShowCodeAsync() => nodeViewModelService.ShowCodeAsync(Node);


        public void ShowDependencies() => nodeViewModelService.ShowReferences(this);


        public void UpdateToolTip()
        {
            string description = GetToolTipDescription();
            string isHiddenText = IsHidden ? "\n\nNode is hidden, Select 'Show Node' in the menu button" : "";
            ToolTip =
                $"{Node.Name.DisplayLongName}" +
                (description != null ? $"\n{description}" : null) +
                isHiddenText +
                $"{DebugToolTip}";
        }


        private string GetToolTipDescription()
        {
            if (!string.IsNullOrEmpty(Node.Description))
            {
                return Node.Description;
            }

            switch (Node.NodeType)
            {
                case NodeType.None:
                    return null;
                case NodeType.Solution:
                    return "Solution";
                case NodeType.SolutionFolder:
                    return "Solution folder";
                case NodeType.Assembly:
                    return "Assembly";
                case NodeType.Group:
                    return "Group";
                case NodeType.Dll:
                    return "Assembly dll";
                case NodeType.Exe:
                    return "Assembly exe";
                case NodeType.NameSpace:
                    return "Namespace";
                case NodeType.Type:
                    return "Type";
                case NodeType.Member:
                    return "Type member";
                case NodeType.PrivateMember:
                    return "Type member";
            }

            return null;
        }


        public void MouseClicked(MouseButtonEventArgs e)
        {
            nodeViewModelService.MouseClicked(this);
        }


        public override void MoveItem(Vector moveOffset)
        {
            Point newLocation = ItemBounds.Location + moveOffset;

            SetBounds(new Rect(newLocation, ItemBounds.Size), false);
            nodeViewModelService.SetIsChanged(Node);
        }


        public void UpdateBounds(Rect bounds)
        {
            SetBounds(bounds, true);
            nodeViewModelService.SetIsChanged(Node);
        }


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


        public void OnMouseWheel(UIElement uiElement, MouseWheelEventArgs e) =>
            nodeViewModelService.OnMouseWheel(this, uiElement, e);
    }
}
