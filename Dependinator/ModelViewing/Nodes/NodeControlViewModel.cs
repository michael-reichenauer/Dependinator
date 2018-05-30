using System;
using System.Linq;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using Dependinator.Common.ThemeHandling;
using Dependinator.ModelViewing.Items;
using Dependinator.Utils.UI.Mvvm;


namespace Dependinator.ModelViewing.Nodes
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

			ItemBounds = nodeViewModel.ItemBounds;

			WhenSet(nodeViewModel, nameof(nodeViewModel.ItemBounds))
				.Notify(_ => ItemBounds = this.nodeViewModel.ItemBounds);

			IsShowControls = true;
		}



		public override double ItemTop => ItemBounds.Top - Margin;
		public override double ItemLeft => ItemBounds.Left - Margin;
		public override double ItemWidth => ItemBounds.Width + 2 * Margin;
		public override double ItemHeight => ItemBounds.Height + 2 * Margin;

		public override bool CanShow => true;
		public bool HasCode => nodeViewModel.HasCode;

		public Brush Brush => ControlBrush;
		public double Margin => MarginPoints / ItemScale;
		public double CenterMargin => MarginPoints;

		public bool IsShowControls { get => Get(); set => Set(value); }


		public bool IsShowBorder { get => Get(); set => Set(value); }

		public string EditModeToolTop => "Toggle edit mode to\nzoom and pan node canvas";
		public string HideToolTop => "Hide node\n(Use application menu to show node)";


		public bool CanShowEditNode =>
			nodeViewModel.Node.Children.Any() && nodeViewModel.CanShowChildren;


		public void OnMouseWheel(UIElement uiElement, MouseWheelEventArgs e) =>
			nodeViewModel.OnMouseWheel(uiElement, e);


		public void Clicked(MouseButtonEventArgs e) => nodeViewModel.MouseClicked(e);

		public Command IncreaseCommand => Command(() => ResizeNode(1.3));
		public Command DecreaseCommand => Command(() => ResizeNode(1 / 1.3));

		public Command ToggleEditModeCommand => Command(ToggleEditNode);
		public Command ShowDependenciesCommand => Command(nodeViewModel.ShowDependencies);
		public Command ShowCodeCommand => Command(nodeViewModel.ShowCode);



		private void ResizeNode(double i)
		{
			Point location = nodeViewModel.ItemBounds.Location;
			Size size = nodeViewModel.ItemBounds.Size;

			Size newSize = new Size(size.Width * i, size.Height * i);
			if (newSize.Width < MinSize || newSize.Height < MinSize)
			{
				// Node to small
				return;
			}

			Point newLocation = new Point(location.X - (newSize.Width - size.Width) / 2, location.Y - (newSize.Height - size.Height) / 2);
			Rect newBounds = new Rect(newLocation, newSize);

			nodeViewModel.ItemBounds = newBounds;

			nodeViewModel.NotifyAll();
			//Vector newCanvasMove = (location - newLocation) * ItemScale;
			//nodeViewModel.ItemsViewModel?.MoveCanvas(newCanvasMove);
			ItemOwnerCanvas.UpdateItem(nodeViewModel);

			nodeViewModel.ItemsViewModel.Zoom(i, new Point(0, 0));
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
			ItemBounds = new Rect(newLocation, ItemBounds.Size);
		}


		public void Move(NodeControl control, Vector viewOffset)
		{
			Vector offset = new Vector(viewOffset.X / ItemScale, viewOffset.Y / ItemScale);

			Point newLocation = GetMoveData(control, offset, out Size newSize, out Vector newCanvasMove);

			if (newSize.Width < MinSize || newSize.Height < MinSize)
			{
				// Node to small
				return;
			}

			Rect newBounds = new Rect(newLocation, newSize);

			if (!nodeViewModel.ItemOwnerCanvas.IsNodeInViewBox(newBounds))
			{
				// Node (if moved) would not bew withing visibale view
				return;
			}

			nodeViewModel.ItemBounds = newBounds;

			nodeViewModel.NotifyAll();
			nodeViewModel.ItemsViewModel?.MoveCanvas(newCanvasMove);
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
					newLocation = new Point((int)newLocation.X, (int)newLocation.Y);
					newSize = size;
					newCanvasMove = new Vector(0, 0);
					break;
				case NodeControl.LeftTop:
					newLocation = location + offset;
					newSize = new Size(size.Width - offset.X, size.Height - offset.Y);
					newCanvasMove = new Vector(-offset.X * ItemScale, -offset.Y * ItemScale);
					break;
				case NodeControl.LeftBottom:
					newLocation = location + new Vector(offset.X, 0);
					newSize = new Size(size.Width - offset.X, size.Height + offset.Y);
					newCanvasMove = new Vector(-offset.X * ItemScale, 0);
					break;
				case NodeControl.RightTop:
					newLocation = location + new Vector(0, offset.Y);
					newSize = new Size(size.Width + offset.X, size.Height - offset.Y);
					newCanvasMove = new Vector(0, -offset.Y * ItemScale);
					break;
				case NodeControl.RightBottom:
					newLocation = location;
					newSize = new Size(size.Width + offset.X, size.Height + offset.Y);
					newCanvasMove = new Vector(0, 0);
					break;
				case NodeControl.Top:
					newLocation = location + new Vector(0, offset.Y);
					newSize = new Size(size.Width, size.Height - offset.Y);
					newCanvasMove = new Vector(0, -offset.Y * ItemScale);
					break;
				case NodeControl.Left:
					newLocation = location + new Vector(offset.X, 0);
					newSize = new Size(size.Width - offset.X, size.Height);
					newCanvasMove = new Vector(-offset.X * ItemScale, 0);
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