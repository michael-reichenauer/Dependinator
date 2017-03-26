using System;
using System.CodeDom;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;


namespace Dependiator.Modeling
{
	/// <summary>
	/// Interaction logic for CompositeNodeView.xaml
	/// </summary>
	public partial class CompositeNodeView : UserControl
	{
		private Point lastMousePosition;

		public CompositeNodeView()
		{
			InitializeComponent();
		}


		protected override void OnMouseWheel(MouseWheelEventArgs e)
		{
			if (!Keyboard.Modifiers.HasFlag(ModifierKeys.Control))
			{
				return;
			}

			CompositeNodeViewModel viewModel = DataContext as CompositeNodeViewModel;
			if (viewModel == null)
			{
				return;
			}

			int zoomDelta = e.Delta;
			Point viewPosition = e.GetPosition(NodesView.ItemsListBox);

			if (e.OriginalSource is ListBox)
			{
				viewModel.Zoom(zoomDelta, viewPosition);
			}
			else
			{
				viewModel.Resize(zoomDelta, viewPosition);
			}
		
					

			e.Handled = true;
		}


		protected override void OnMouseMove(MouseEventArgs e)
		{
			CompositeNodeViewModel viewModel = DataContext as CompositeNodeViewModel;
			if (viewModel == null)
			{
				return;
			}

			Point viewPosition = e.GetPosition(viewModel.ParentView);

			if (Keyboard.Modifiers.HasFlag(ModifierKeys.Control)
				&& e.LeftButton == MouseButtonState.Pressed
				&& !(e.OriginalSource is Thumb)) // Don't block the scrollbars.
			{
				// Move node
				CaptureMouse();
				Vector viewOffset = viewPosition - lastMousePosition;
				e.Handled = true;

				viewModel.MoveNode(viewOffset);
			}
			else
			{
				// End of move
				ReleaseMouseCapture();
			}

			lastMousePosition = viewPosition;
		}




		private void UIElement_OnMouseWheel(object sender, MouseWheelEventArgs e)
		{
			// No needed since the OnMouseWheel() handles this event.
		}
	}
}
