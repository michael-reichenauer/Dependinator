using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;


namespace Dependiator.Modeling
{
	/// <summary>
	/// Interaction logic for NodesNodeView.xaml
	/// </summary>
	public partial class NodesNodeView : UserControl
	{
		public NodesNodeView()
		{
			InitializeComponent();
		}


		private void UIElement_OnMouseWheel(object sender, MouseWheelEventArgs e)
		{
			if (!Keyboard.Modifiers.HasFlag(ModifierKeys.Control))
			{
				return;
			}

			NodesNodeViewModel viewModel = DataContext as NodesNodeViewModel;
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
