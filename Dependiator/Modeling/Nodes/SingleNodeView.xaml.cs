using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;


namespace Dependiator.Modeling.Nodes
{
	/// <summary>
	/// Interaction logic for SingleNodeView.xaml
	/// </summary>
	public partial class SingleNodeView : UserControl
	{
		private static readonly double ZoomSpeed = 3000.0;

		private Point lastMousePosition;

		public SingleNodeView()
		{
			InitializeComponent();
		}


		protected override void OnMouseMove(MouseEventArgs e)
		{
			SingleNodeViewModel viewModel = DataContext as SingleNodeViewModel;
			if (viewModel == null)
			{
				return;
			}

			Point viewPosition = e.GetPosition(Application.Current.MainWindow);

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

		protected override void OnMouseWheel(MouseWheelEventArgs e)
		{
			if (!Keyboard.Modifiers.HasFlag(ModifierKeys.Control))
			{
				return;
			}

			if (!Keyboard.Modifiers.HasFlag(ModifierKeys.Shift))
			{
				return;
			}

			if (!(DataContext is SingleNodeViewModel viewModel))
			{
				return;
			}

			int wheelDelta = e.Delta;
			Point viewPosition = e.GetPosition(this);
		
			double zoom = Math.Pow(2, wheelDelta / ZoomSpeed);

			viewModel.ZoomLinks(zoom, viewPosition);
					

			e.Handled = true;
		}

	}
}
