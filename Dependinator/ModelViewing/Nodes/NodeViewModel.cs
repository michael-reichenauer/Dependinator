using System;
using System.Windows;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
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
		}

		protected override Rect GetItemBounds() => node.NodeBounds;

		public Brush RectangleBrush => node.GetNodeBrush();
		public Brush BackgroundBrush => node.GetBackgroundNodeBrush();

		public string Name => node.NodeName.ShortName;

		public string ToolTip => $"{node.NodeName}{node.DebugToolTip}";


		public void UpdateToolTip() => Notify(nameof(ToolTip));

		public int FontSize => ((int)(15 * node.NodeScale)).MM(8, 13);

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

				node.Move(viewOffset, null, false);
			}
			else
			{
				// End of move
				(e.Source as UIElement)?.ReleaseMouseCapture();
			}

			lastMousePosition = viewPosition;
		}

		public override string ToString() => node.NodeName;
	}
}