using System;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using Dependinator.Common.ThemeHandling;
using Dependinator.ModelViewing.Items;


namespace Dependinator.ModelViewing.Nodes
{
	internal class NodePointsView2Model : ItemViewModel
	{
		private static readonly int MarginPoints = 10;
		private static readonly SolidColorBrush ControlBrush = Converter.BrushFromHex("#FFB0C4DE");

		private readonly NodeViewModel nodeViewModel;


		public NodePointsView2Model(NodeViewModel nodeViewModel)
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


		public void MouseDown(Point point)
		{
		}


		public void MouseUp(Point point)
		{
		}





		public void Move(NodeControl control, Vector viewOffset)
		{
			MoveNode(control, viewOffset);
		}


		public void MoveNode(NodeControl control, Vector viewOffset)
		{
			Vector offset = new Vector(viewOffset.X / ItemScale, viewOffset.Y / ItemScale);

			Point location = nodeViewModel.ItemBounds.Location;
			Size size = nodeViewModel.ItemBounds.Size;

			Point newLocation;
			Size newSize;
			Vector newCanvasMove;

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


			nodeViewModel.ItemBounds = new Rect(newLocation, newSize);
			nodeViewModel.NotifyAll();
			nodeViewModel.ItemsViewModel?.MoveCanvas(newCanvasMove);
			ItemOwnerCanvas.UpdateItem(nodeViewModel);
		}


		public void Clicked(MouseButtonEventArgs e)
		{
			IsShowControls = false;
			IsShowBorder = true;
		}
	}
}