using System;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using Dependinator.Modeling;
using Dependinator.ModelViewing.Private.Items;

namespace Dependinator.ModelViewing.Nodes
{
	internal class NodeViewModel : ItemViewModel
	{
		private Point lastMousePosition;
		private readonly Node node;

		protected NodeViewModel(Node node)
		{
			this.node = node;

			NodeBounds = new Rect(200, 200, 250, 150);
			NodeScale = 1;
		}

		protected override Rect GetItemBounds() => NodeBounds;
		public Rect NodeBounds { get; private set; }
		public double NodeScale { get; private set; }

		public Brush RectangleBrush => Brushes.Aqua;
		public Brush BackgroundBrush => Brushes.DimGray;

		public string Name => node.Name.ShortName;

		public string ToolTip => $"{node.Name}{DebugToolTip}";

		public void UpdateToolTip() => Notify(nameof(ToolTip));

		public int FontSize => ((int)(15 * NodeScale)).MM(8, 13);


		public void OnMouseMove(MouseEventArgs e)
		{
			Point viewPosition = e.GetPosition(Application.Current.MainWindow);

			//if (Keyboard.Modifiers.HasFlag(ModifierKeys.Control)
			//    && e.LeftButton == MouseButtonState.Pressed
			//    && !(e.OriginalSource is Thumb)) // Don't block the scrollbars.
			//{

			//	// Move node
			//	(e.Source as UIElement)?.CaptureMouse();
			//	Vector viewOffset = viewPosition - lastMousePosition;
			//	e.Handled = true;

			//	node.Move(viewOffset, null, false);
			//}
			//else
			//{
			//	// End of move
			//	(e.Source as UIElement)?.ReleaseMouseCapture();
			//}

			lastMousePosition = viewPosition;
		}

		public string DebugToolTip => ItemsToolTip;


		public string ItemsToolTip =>
			$"\nRect: {NodeBounds.TS()}\n";


		public override string ToString() => node.Name;
	}
}