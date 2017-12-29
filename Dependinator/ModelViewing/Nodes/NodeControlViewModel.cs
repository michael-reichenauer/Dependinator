using System;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using Dependinator.Common.ThemeHandling;
using Dependinator.ModelViewing.Items;


namespace Dependinator.ModelViewing.Nodes
{
	internal class NodeControlViewModel : ItemViewModel
	{
		private static readonly int MarginPoints = 10;
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

		public Brush Brush => ControlBrush;
		public double Margin => MarginPoints / ItemScale;
		public double CenterMargin => MarginPoints;

		public override void MoveItem(Vector moveOffset)
		{
			Point newLocation = ItemBounds.Location + moveOffset;
			ItemBounds = new Rect(newLocation, ItemBounds.Size);
		}

		public bool IsShowControls { get => Get(); set => Set(value); }
		public bool IsShowBorder { get => Get(); set => Set(value); }

		public bool CanShowEditNode => nodeViewModel.CanShowChildren;


		public void OnMouseWheel(UIElement uiElement, MouseWheelEventArgs e) => 
			nodeViewModel.OnMouseWheel(uiElement, e);


		public void ClickedEditNode(MouseButtonEventArgs e)
		{
			if (!IsShowBorder)
			{
				IsShowControls = false;
				IsShowBorder = true;
				nodeViewModel.IsInnerSelected = true;
			}
			else
			{
				IsShowControls = true;
				IsShowBorder = false;
				nodeViewModel.IsInnerSelected = false;
			}
		}


		public void Clicked(MouseButtonEventArgs e)
		{
			nodeViewModel.MouseClickedUnselect(e);
		}


		public void Move(NodeControl control, Vector viewOffset)
		{
			Vector offset = new Vector(viewOffset.X / ItemScale, viewOffset.Y / ItemScale);
			
			Point newLocation = GetMoveData(control, offset, out Size newSize, out Vector newCanvasMove);

			if (newSize.Width < 50 || newSize.Height < 50)
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
	}
}