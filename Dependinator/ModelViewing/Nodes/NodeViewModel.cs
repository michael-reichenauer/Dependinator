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

		private Point lastMousePosition;
		private int currentPointIndex = -1;
		private Point mouseDownPoint;

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


		public void OnMouseMove(MouseEventArgs e)
		{
			Point viewPosition = e.GetPosition(Application.Current.MainWindow);

			if (Keyboard.Modifiers.HasFlag(ModifierKeys.Control)
					&& e.LeftButton == MouseButtonState.Pressed
					&& !(e.OriginalSource is Thumb)) // Don't block the scrollbars.
			{
				// Move node
				(e.Source as UIElement)?.CaptureMouse();
				Vector viewOffset = viewPosition - lastMousePosition;
				e.Handled = true;

				Move(viewOffset);
			}
			else
			{
				// End of move
				(e.Source as UIElement)?.ReleaseMouseCapture();
			}

			lastMousePosition = viewPosition;
		}



		private void Move(Vector viewOffset)
		{
			Vector scaledOffset = viewOffset / ItemScale;

			Point newLocation = ItemBounds.Location + scaledOffset;
			Size size = ItemBounds.Size;

			ItemBounds = new Rect(newLocation, size);
			ItemOwnerCanvas.UpdateItem(this);
		}


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
				return 0;
			}
			else if ((point - new Point(
				viewModel.ItemLeft + viewModel.ItemWidth,
				viewModel.ItemTop)).Length < dist)
			{

				return 1;
			}
			else if ((point - new Point(
				viewModel.ItemLeft + viewModel.ItemWidth,
				viewModel.ItemTop + viewModel.ItemHeight)).Length < dist)
			{

				return 2;
			}
			else if ((point - new Point(
				viewModel.ItemLeft,
				viewModel.ItemTop + viewModel.ItemHeight)).Length < dist)
			{

				return 3;
			}

			return -1;
		}




		private void MovePoint(Node node, int index, Point point)
		{
			NodeViewModel viewModel = node.ViewModel;

			Point loc = viewModel.ItemBounds.Location;
			Size size = viewModel.ItemBounds.Size;

			if (index == 0)
			{
				Point newLoc = new Point(point.X, point.Y);
				double xd = loc.X - newLoc.X;
				double yd = loc.Y - newLoc.Y;

				Size newSiz = new Size(xd + size.Width, yd + size.Height);
				viewModel.ItemBounds = new Rect(newLoc, newSiz);
			}
			else if (index == 1)
			{
				Point newLoc = new Point(point.X - size.Width, point.Y);
				double xd = loc.X - newLoc.X;
				double yd = loc.Y - newLoc.Y;

				Size newSiz = new Size(xd + size.Width, yd + size.Height);

				viewModel.ItemBounds = new Rect(newLoc, newSiz);
			}

			else if (index == 2)
			{
				viewModel.ItemBounds = new Rect(
					viewModel.ItemBounds.Location,
					new Size(point.X - viewModel.ItemLeft, point.Y - viewModel.ItemTop));
			}
			else if (index == 3)
			{
				Point newLoc = new Point(point.X, point.Y - size.Height);
				Size newSiz = new Size(
					viewModel.ItemWidth + (loc.X - newLoc.X),
					(viewModel.ItemHeight - (loc.Y - newLoc.Y)));

				viewModel.ItemBounds = new Rect(newLoc, newSiz);
			}
		}



		public void MouseUp(Point screenPoint)
		{
			currentPointIndex = -1;
		}
	}
}