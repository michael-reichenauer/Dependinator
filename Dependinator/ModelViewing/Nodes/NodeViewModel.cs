using System;
using System.Windows;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using Dependinator.Modeling;
using Dependinator.ModelViewing.Private.Items;

namespace Dependinator.ModelViewing.Nodes
{
	internal abstract class NodeViewModel : ItemViewModel
	{
		private readonly INodeService nodeService;

		private Point lastMousePosition;
		private readonly Node node;

		public NodeViewModel(INodeService nodeService, Node node)
		{
			this.nodeService = nodeService;
			this.node = node;
			RectangleBrush = nodeService.GetRandomRectangleBrush();
			BackgroundBrush = nodeService.GetRectangleBackgroundBrush(RectangleBrush);
		}

		public override bool CanShow => ItemScale > 0.15;

		public NodeId NodeId => node.Id;

		public Brush RectangleBrush { get; }
		public Brush BackgroundBrush { get; }
	

		public string Name => node.Name.ShortName;

		public string ToolTip => $"{node.Name}{DebugToolTip}";

		public void UpdateToolTip() => Notify(nameof(ToolTip));

		public int FontSize => ((int)(15 * ItemScale)).MM(8, 13);



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


		public override string ToString() => node.Name;
	}
}