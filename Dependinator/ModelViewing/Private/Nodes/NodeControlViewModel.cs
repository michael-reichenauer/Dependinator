using System;
using System.Linq;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using Dependinator.Common.ThemeHandling;
using Dependinator.ModelViewing.Private.ItemsViewing;
using Dependinator.Utils.UI.Mvvm;


namespace Dependinator.ModelViewing.Private.Nodes
{
    internal class NodeControlViewModel : ItemViewModel
    {
        private static readonly int MarginPoints = 50;
        private static readonly double MinSize = 30.0;
        private static readonly SolidColorBrush ControlBrush = Converter.BrushFromHex("#FFB0C4DE");

        private readonly NodeViewModel nodeViewModel;


        public NodeControlViewModel(NodeViewModel nodeViewModel)
        {
            this.nodeViewModel = nodeViewModel;

            SetControlBounds(nodeViewModel.ItemBounds, false);

            WhenSet(nodeViewModel, nameof(nodeViewModel.ItemBounds))
                .Notify(_ => SetControlBounds(this.nodeViewModel.ItemBounds, true));

            IsShowControls = true;
        }


        public override double ItemTop => ItemBounds.Top - Margin;
        public override double ItemLeft => ItemBounds.Left - Margin;
        public override double ItemWidth => ItemBounds.Width + 2 * Margin;
        public override double ItemHeight => ItemBounds.Height + 2 * Margin;

        public override bool CanShow => nodeViewModel.CanShow;
        public bool HasCode => nodeViewModel.HasCode;

        public Brush Brush => ControlBrush;
        public double Margin => MarginPoints / ItemScale;
        public double CenterMargin => MarginPoints;

        public bool IsShowControls { get => Get(); set => Set(value); }


        public bool IsShowBorder { get => Get(); set => Set(value); }

        public string EditModeToolTop => "Toggle edit mode to\nzoom and pan node canvas";
        public string HideToolTop => "Hide node\n(Use application menu to show node)";

        public bool IsHidden => nodeViewModel.IsHidden;

        public bool CanShowEditNode =>
            !nodeViewModel.IsHidden &&
            nodeViewModel.Node.Children.Any() &&
            nodeViewModel.CanShowChildren;

        public Command IncreaseCommand => Command(() => ResizeNode(1.3));
        public Command DecreaseCommand => Command(() => ResizeNode(1 / 1.3));

        public Command HideNodeCommand => Command(() => nodeViewModel.HideNode());
        public Command ShowNodeCommand => Command(() => nodeViewModel.ShowNode());


        public Command ToggleEditModeCommand => Command(ToggleEditNode);
        public Command ShowDependenciesCommand => Command(nodeViewModel.ShowDependencies);
        public Command ShowCodeCommand => AsyncCommand(nodeViewModel.ShowCodeAsync);
        public Command RearrangeLayoutCommand => Command(nodeViewModel.RearrangeLayout);


        public void OnMouseWheel(UIElement uiElement, MouseWheelEventArgs e) =>
            nodeViewModel.OnMouseWheel(uiElement, e);


        public void Clicked(MouseButtonEventArgs e) => nodeViewModel.MouseClicked(e);


        private void ResizeNode(double i)
        {
            Point location = nodeViewModel.ItemBounds.Location;
            Size size = nodeViewModel.ItemBounds.Size;

            Size newSize = new Size((size.Width * i).RoundToNearest(5), (size.Height * i).RoundToNearest(5));
            if (newSize.Width < MinSize || newSize.Height < MinSize)
            {
                // Node to small
                return;
            }

            Point newLocation = new Point(location.X - (newSize.Width - size.Width) / 2,
                location.Y - (newSize.Height - size.Height) / 2);
            newLocation = newLocation.Rnd(5);
            Rect newBounds = new Rect(newLocation, newSize);

            nodeViewModel.UpdateBounds(newBounds);

            nodeViewModel.NotifyAll();
            ItemOwnerCanvas.UpdateItem(nodeViewModel);
            nodeViewModel.ItemsViewModel?.Zoom(i, new Point(0, 0));
        }


        public void ToggleEditNode()
        {
            if (!IsShowBorder)
            {
                EnableEditNodeContents();
            }
            else
            {
                DeselectNode();
            }
        }


        public override void MoveItem(Vector moveOffset)
        {
            Point newLocation = ItemBounds.Location + moveOffset;
            SetBounds(new Rect(newLocation, ItemBounds.Size), false);
        }


        public void Move(NodeControl control, Vector viewOffset, Point sp1, Point sp2)
        {
            double roundTo = nodeViewModel.ItemParentScale < 10 ? 5 : 1;
            Point cp1 = nodeViewModel.ItemOwnerCanvas.ScreenToCanvasPoint(sp1).Rnd(roundTo);
            Point cp2 = nodeViewModel.ItemOwnerCanvas.ScreenToCanvasPoint(sp2).Rnd(roundTo);

            Vector offset = cp2 - cp1;
            Point newLocation = GetMoveData(control, offset, out Size newSize, out Vector newCanvasMove);

            if (newSize.Width < MinSize || newSize.Height < MinSize)
            {
                // Node to small
                return;
            }

            Rect newBounds = new Rect(newLocation, newSize);

            if (!nodeViewModel.ItemOwnerCanvas.IsNodeInViewBox(newBounds))
            {
                // Node (if moved) would not bew withing visible view
                return;
            }

            nodeViewModel.UpdateBounds(newBounds);

            nodeViewModel.NotifyAll();

            if (control != NodeControl.Center)
            {
               nodeViewModel.ItemsViewModel?.MoveItems(newCanvasMove);
            }

            ItemOwnerCanvas.UpdateItem(nodeViewModel);
        }


        private Point GetMoveData(
            NodeControl control,
            Vector offset,
            out Size newSize,
            out Vector newCanvasMove)
        {
            Point location = nodeViewModel.ItemBounds.Location;
            Size size = nodeViewModel.ItemBounds.Size;

            Point newLocation;
            switch (control)
            {
                case NodeControl.Center:
                    newLocation = location + offset;
                    newSize = size;
                    newCanvasMove = new Vector(0, 0);
                    break;
                case NodeControl.LeftTop:
                    newLocation = location + offset;
                    newSize = new Size(size.Width - offset.X, size.Height - offset.Y);
                    newCanvasMove = new Vector(-offset.X, -offset.Y);
                    break;
                case NodeControl.LeftBottom:
                    newLocation = location + new Vector(offset.X, 0);
                    newSize = new Size(size.Width - offset.X, size.Height + offset.Y);
                    newCanvasMove = new Vector(-offset.X, 0);
                    break;
                case NodeControl.RightTop:
                    newLocation = location + new Vector(0, offset.Y);
                    newSize = new Size(size.Width + offset.X, size.Height - offset.Y);
                    newCanvasMove = new Vector(0, -offset.Y);
                    break;
                case NodeControl.RightBottom:
                    newLocation = location;
                    newSize = new Size(size.Width + offset.X, size.Height + offset.Y);
                    newCanvasMove = new Vector(0, 0);
                    break;
                case NodeControl.Top:
                    newLocation = location + new Vector(0, offset.Y);
                    newSize = new Size(size.Width, size.Height - offset.Y);
                    newCanvasMove = new Vector(0, -offset.Y);
                    break;
                case NodeControl.Left:
                    newLocation = location + new Vector(offset.X, 0);
                    newSize = new Size(size.Width - offset.X, size.Height);
                    newCanvasMove = new Vector(-offset.X, 0);
                    break;
                case NodeControl.Right:
                    newLocation = location + new Vector(0, 0);
                    newSize = new Size(size.Width + offset.X, size.Height);
                    newCanvasMove = new Vector(0, 0);
                    break;
                case NodeControl.Bottom:
                    newLocation = location;
                    newSize = new Size(size.Width, size.Height + offset.Y);
                    newCanvasMove = new Vector(0, 0);
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(control), control, null);
            }

            return newLocation;
        }



        private void SetControlBounds(Rect bounds, bool isUpdateParent)
        {
            if (nodeViewModel.Node.Parent.IsRoot)
            {
                // Node owner is root, no need to transform
                SetBounds(bounds, isUpdateParent);
                return;
            }

            // The item bounds are translated to the root canvas coordinate system
            Point screenLocation = nodeViewModel.Node.Parent.ItemsCanvas.CanvasToScreenPoint2(bounds.Location);
            Point rootLocation = nodeViewModel.Node.Root.ItemsCanvas.ScreenToCanvasPoint(screenLocation);

            double scale = nodeViewModel.Node.Root.ItemsCanvas.Scale;
            Size rootSize = new Size(
                (bounds.Size.Width * nodeViewModel.ItemScale) / scale,
                (bounds.Size.Height * nodeViewModel.ItemScale) / scale);

            Rect rootBounds = new Rect(rootLocation, rootSize);
            SetBounds(rootBounds, isUpdateParent);
        }


        private void DeselectNode()
        {
            IsShowControls = true;
            IsShowBorder = false;
            nodeViewModel.IsInnerSelected = false;

            if (nodeViewModel.ItemsViewModel?.ItemsCanvas != null)
            {
                nodeViewModel.ItemsViewModel.ItemsCanvas.IsFocused = false;
            }
        }


        private void EnableEditNodeContents()
        {
            IsShowControls = false;
            IsShowBorder = true;
            nodeViewModel.IsInnerSelected = true;
            if (nodeViewModel.ItemsViewModel?.ItemsCanvas != null)
            {
                nodeViewModel.ItemsViewModel.ItemsCanvas.IsFocused = true;
            }
        }
    }
}
