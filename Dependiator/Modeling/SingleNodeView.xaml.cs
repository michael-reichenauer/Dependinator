using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;


namespace Dependiator.Modeling
{
	/// <summary>
	/// Interaction logic for SingleNodeView.xaml
	/// </summary>
	public partial class SingleNodeView : UserControl
	{
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

			if (Keyboard.Modifiers.HasFlag(ModifierKeys.Alt))
			{
				viewModel.NotifyAll();
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

	}
}
