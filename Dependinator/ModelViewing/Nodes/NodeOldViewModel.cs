using System;
using System.Windows;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using Dependinator.ModelViewing.Private.Items;

namespace Dependinator.ModelViewing.Nodes
{
	internal class NodeOldViewModel : ItemViewModel
	{
		private Point lastMousePosition;
		private readonly NodeOld node;

		protected NodeOldViewModel(NodeOld node)
		{
			this.node = node;
		}

		protected Rect GetItemBounds() => node.ItemBounds;

		public Brush RectangleBrush => node.GetNodeBrush();
		public Brush BackgroundBrush => node.GetBackgroundNodeBrush();

		public string Name => node.NodeName.ShortName;

		public string ToolTip => $"{node.NodeName}{DebugToolTip}";


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

		public string DebugToolTip => ItemsToolTip;

		//public string ItemsToolTip =>
		//	$"\nChildren: {ChildNodes.Count}, Lines: {Links.OwnedLines.Count}\n" +
		//	$"Total Nodes: {CountShowingNodes()}, Lines: {CountShowingLines()}\n" +
		//	$"Node Scale: {NodeScale:0.00}, Items Scale: {ItemsScale:0.00}, (Factor: {ItemsScaleFactor:0.00})\n" +
		//	$"Rect: {NodeBounds.TS()}\n";

		public string ItemsToolTip =>
			$"\nRect: {node.ItemBounds.TS()}\n";


		public override string ToString() => node.NodeName;
	}
}