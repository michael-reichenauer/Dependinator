using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;


namespace Dependiator.Modeling.Nodes
{
	/// <summary>
	/// Interaction logic for MemberNodeView.xaml
	/// </summary>
	public partial class MemberNodeView : UserControl
	{
		private Point lastMousePosition;

		private MemberNodeViewModel ViewModel => DataContext as MemberNodeViewModel;

		public MemberNodeView()
		{
			InitializeComponent();
		}

		protected override void OnMouseMove(MouseEventArgs e)
		{
			Point viewPosition = e.GetPosition(Application.Current.MainWindow);

			if (Keyboard.Modifiers.HasFlag(ModifierKeys.Control)
			    && e.LeftButton == MouseButtonState.Pressed
			    && !(e.OriginalSource is Thumb)) // Don't block the scrollbars.
			{
				// Move node
				CaptureMouse();
				Vector viewOffset = viewPosition - lastMousePosition;
				e.Handled = true;

				ViewModel?.MoveNode(viewOffset);
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
