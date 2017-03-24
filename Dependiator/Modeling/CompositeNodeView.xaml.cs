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


		protected override void OnPreviewMouseWheel(MouseWheelEventArgs e)
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

			if (Keyboard.Modifiers.HasFlag(ModifierKeys.Shift))
			{
				viewModel.Resize(zoomDelta, viewPosition);
			}
			else
			{
				viewModel.Zoom(zoomDelta, viewPosition);				
			}

			e.Handled = true;
		}

		protected override void OnPreviewMouseMove(MouseEventArgs e)
		{
			Point viewPosition = e.GetPosition(NodesView.ItemsListBox);

			if (Keyboard.Modifiers.HasFlag(ModifierKeys.Control)
				&& e.LeftButton == MouseButtonState.Pressed
				&& !(e.OriginalSource is Thumb)) // Don't block the scrollbars.
			{
				CompositeNodeViewModel viewModel = DataContext as CompositeNodeViewModel;
				if (viewModel == null)
				{
					return;
				}

				// Move canvas
				CaptureMouse();
				Vector viewOffset = viewPosition - lastMousePosition;
				e.Handled = viewModel.MoveCanvas(viewOffset);
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
			Point viewPosition = e.GetPosition(sender as IInputElement);

			if (Keyboard.Modifiers.HasFlag(ModifierKeys.Shift))
			{
				viewModel.Zoom(zoomDelta, viewPosition);
			}
			else
			{
				viewModel.Resize(zoomDelta, viewPosition);
			}

			e.Handled = true;
		}
	}
}
