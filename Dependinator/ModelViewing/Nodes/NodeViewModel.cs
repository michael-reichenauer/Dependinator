using System;
using System.Windows;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using Dependinator.ModelViewing.Private.Items;

namespace Dependinator.ModelViewing.Nodes
{
	internal abstract class NodeViewModel : ItemViewModel
	{
		private readonly INodeViewModelService nodeViewModelService;

		private Point lastMousePosition;

		protected NodeViewModel(INodeViewModelService nodeViewModelService, Node node)
		{
			this.nodeViewModelService = nodeViewModelService;
			this.Node = node;
			RectangleBrush = nodeViewModelService.GetRandomRectangleBrush();
			BackgroundBrush = nodeViewModelService.GetRectangleBackgroundBrush(RectangleBrush);
		}

		public override bool CanShow => ItemScale > 0.15;

		public Node Node { get; }

		public Brush RectangleBrush { get; }
		public Brush BackgroundBrush { get; }


		public string Name => Node.Name.ShortName;

		public string ToolTip => $"{Node.Name}{DebugToolTip}";

		public void UpdateToolTip() => Notify(nameof(ToolTip));

		public int FontSize => ((int)(15 * ItemScale)).MM(8, 13);

		public ItemsViewModel ItemsViewModel { get; set; }

		public override void ItemRealized()
		{
			base.ItemRealized();

			// If this node has an items canvas, make sure it knows it has been realized (fix zoom level)
			ItemsViewModel?.ItemRealized();
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


		public string ItemsToolTip => "\n" +
			$"Rect: {ItemBounds.TS()}\n" +
			$"Scale {ItemScale}\n" +
			$"Items: {ItemOwnerCanvas.CanvasRoot.AllItemsCount()}, Shown {ItemOwnerCanvas.CanvasRoot.ShownItemsCount()}";


		public override string ToString() => Node.Name.ToString();
	}
}