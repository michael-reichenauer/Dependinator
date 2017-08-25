using System;
using System.Windows;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using Dependinator.Common.ThemeHandling;
using Dependinator.ModelViewing.Private.Items;
using Dependinator.Utils.UI;

namespace Dependinator.ModelViewing.Nodes
{
	internal abstract class NodeViewModel : ItemViewModel
	{
		private readonly INodeViewModelService nodeViewModelService;

		private readonly DelayDispatcher mouseOverDelay = new DelayDispatcher();

		//private Point lastMousePosition;
		private int currentPointIndex = -1;
		private Point mouseDownPoint;
		private Point mouseMovedPoint;

		protected NodeViewModel(INodeViewModelService nodeViewModelService, Node node)
		{
			this.nodeViewModelService = nodeViewModelService;
			this.Node = node;

			RectangleBrush = node.Color != null
				? Converter.BrushFromHex(node.Color)
				: nodeViewModelService.GetRandomRectangleBrush();
			BackgroundBrush = nodeViewModelService.GetRectangleBackgroundBrush(RectangleBrush);
		}


		public override bool CanShow => ItemScale > 0.15;

		public Node Node { get; }

		public Brush RectangleBrush { get; }
		public Brush BackgroundBrush { get; }


		public string Name => Node.Name.Name;

		public double RectangleLineWidth => IsMouseOver ? 0.6 * 1.5 : 0.6;

		public string ToolTip => $"{Node.Name}{DebugToolTip}";

		public void UpdateToolTip() => Notify(nameof(ToolTip));

		public int FontSize => ((int)(15 * ItemScale)).MM(9, 13);

		public bool IsMouseOver { get => Get(); private set => Set(value); }
		public bool IsShowPoints { get => Get(); private set => Set(value); }

		public ItemsViewModel ItemsViewModel { get; set; }

		public override void ItemRealized()
		{
			base.ItemRealized();

			// If this node has an items canvas, make sure it knows it has been realized (fix zoom level)
			ItemsViewModel?.ItemRealized();
		}

		public override void ItemVirtualized()
		{
			base.ItemVirtualized();
			ItemsViewModel?.ItemVirtualized();
		}


		//public void OnMouseMove(MouseEventArgs e)
		//{
		//	Point viewPosition = e.GetPosition(Application.Current.MainWindow);

		//	if (Keyboard.Modifiers.HasFlag(ModifierKeys.Control)
		//			&& e.LeftButton == MouseButtonState.Pressed
		//			&& !(e.OriginalSource is Thumb)) // Don't block the scrollbars.
		//	{
		//		// Move node
		//		(e.Source as UIElement)?.CaptureMouse();
		//		Vector viewOffset = viewPosition - lastMousePosition;
		//		e.Handled = true;

		//		Move(viewOffset);
		//	}
		//	else
		//	{
		//		// End of move
		//		(e.Source as UIElement)?.ReleaseMouseCapture();
		//	}

		//	lastMousePosition = viewPosition;
		//}



		//private void Move(Vector viewOffset)
		//{
		//	Vector scaledOffset = viewOffset / ItemScale;

		//	Point newLocation = ItemBounds.Location + scaledOffset;
		//	Size size = ItemBounds.Size;

		//	ItemBounds = new Rect(newLocation, size);
		//	ItemOwnerCanvas.UpdateItem(this);
		//}


		public string DebugToolTip => ItemsToolTip;


		public string ItemsToolTip =>
			"\n" +
			$"Rect: {ItemBounds.TS()}\n" +
			$"Scale {ItemScale}\n";

		public string Color => RectangleBrush.AsString();

		//$"Items: {ItemOwnerCanvas.CanvasRoot.AllItemsCount()}, Shown {ItemOwnerCanvas.CanvasRoot.ShownItemsCount()}";


		public override string ToString() => Node.Name.ToString();

		public void OnMouseEnter()
		{
			mouseOverDelay.Delay(ModelViewModel.MouseEnterDelay, _ =>
			{
				IsMouseOver = true;
				IsShowPoints = Keyboard.Modifiers.HasFlag(ModifierKeys.Control);
				Notify(nameof(RectangleLineWidth));
			});
		}

		public void OnMouseLeave()
		{
			mouseOverDelay.Cancel();
			IsMouseOver = false;
			IsShowPoints = false;
			Notify(nameof(RectangleLineWidth));
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
				currentPointIndex = GetPointIndex(Node, mouseDownPoint);
				if (currentPointIndex == -1)
				{
					// Point not close enough to the line
					return;
				}
			}

			MovePoint(Node, currentPointIndex, point);
			mouseMovedPoint = point;
			IsMouseOver = true;
			IsShowPoints = true;
			NotifyAll();
		}

		private static int GetPointIndex(Node node, Point point)
		{
			int dist = 10;
			NodeViewModel viewModel = node.ViewModel;

			if ((point - viewModel.ItemBounds.Location).Length < dist)
			{
				// Move left,top
				return 1;
			}
			else if ((point - new Point(
				viewModel.ItemLeft + viewModel.ItemWidth,
				viewModel.ItemTop)).Length < dist)
			{
				// Move right,top
				return 2;
			}
			else if ((point - new Point(
				viewModel.ItemLeft + viewModel.ItemWidth,
				viewModel.ItemTop + viewModel.ItemHeight)).Length < dist)
			{
				// Move right,bottom
				return 3;
			}
			else if ((point - new Point(
				viewModel.ItemLeft,
				viewModel.ItemTop + viewModel.ItemHeight)).Length < dist)
			{
				// Move left,bottom
				return 4;
			}

			// Move node
			return 0;
		}




		private void MovePoint(Node node, int index, Point point)
		{
			NodeViewModel viewModel = node.ViewModel;

			Point location = viewModel.ItemBounds.Location;
			Point newLocation = location;

			Size size = viewModel.ItemBounds.Size;
			Vector resize = new Vector(0, 0);

			if (index == 0)
			{
				Vector moved = point - mouseMovedPoint;
				newLocation = location + moved;
			}
			else if (index == 1)
			{
				newLocation = new Point(point.X, point.Y);
				resize = new Vector(location.X - newLocation.X, location.Y - newLocation.Y);
			}
			else if (index == 2)
			{
				newLocation = new Point(location.X, point.Y);
				resize = new Vector((point.X - size.Width) - location.X, location.Y - newLocation.Y);;
			}

			else if (index == 3)
			{
				newLocation = location;
				resize = new Vector((point.X - size.Width) - location.X, (point.Y - size.Height) - location.Y);
			}
			else if (index == 4)
			{
				newLocation = new Point(point.X, location.Y);
				resize = new Vector(location.X - newLocation.X, (point.Y - size.Height) - location.Y);
			}

			Size newSiz = new Size(size.Width + resize.X, size.Height + resize.Y);
			viewModel.ItemBounds = new Rect(newLocation, newSiz);
		}


		public void MouseUp(Point screenPoint) => currentPointIndex = -1;
	}
}