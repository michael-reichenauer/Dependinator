using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using Dependinator.Utils;


namespace Dependinator.Modeling.Nodes
{
	/// <summary>
	/// Interaction logic for CompositeNodeView.xaml
	/// </summary>
	public partial class CompositeNodeView : UserControl
	{
		private static readonly double ZoomSpeed = 3000.0;
		private bool isDoing = false;

		private Point lastMousePosition;
	//	private Point lastMousePosition2;

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

			if (!(DataContext is CompositeNodeViewModel viewModel))
			{
				return;
			}
			

			int wheelDelta = e.Delta;
			Point viewPosition = e.GetPosition(NodesView.ItemsListBox);
			double zoom = Math.Pow(2, wheelDelta / ZoomSpeed);
			if (e.OriginalSource is ListBox)
			{
				viewModel.Zoom(zoom, viewPosition);	
			}
			else
			{			
				viewModel.ZoomResize(wheelDelta);				
			}					

			e.Handled = true;
		}


		protected override void OnMouseMove(MouseEventArgs e)
		{
			if (!(DataContext is CompositeNodeViewModel viewModel))
			{
				return;
			}

			Point viewPosition = e.GetPosition(Application.Current.MainWindow);
			Point viewPosition2 = e.GetPosition(e.OriginalSource as IInputElement);

			if (Keyboard.Modifiers.HasFlag(ModifierKeys.Control)
				&& e.LeftButton == MouseButtonState.Pressed
				&& !(e.OriginalSource is Thumb)) // Don't block the scrollbars.
			{
				
				// Move node
				CaptureMouse();
				Vector viewOffset = viewPosition - lastMousePosition;
				e.Handled = true;

				viewModel.MoveNode(viewOffset, viewPosition2, isDoing);
				isDoing = true;
			}
			else
			{
				// End of move
				isDoing = false;
				ReleaseMouseCapture();
			}

			lastMousePosition = viewPosition;
		}


		private void ToolTip_OnOpened(object sender, RoutedEventArgs e)
		{
			if (DataContext is CompositeNodeViewModel viewModel)
			{
				viewModel.UpdateToolTip();
			}
		}


		//private void ResizeBorder_OnMouseMove(object sender, MouseEventArgs e)
		//{
		//	CompositeNodeViewModel viewModel = DataContext as CompositeNodeViewModel;
		//	if (viewModel == null)
		//	{
		//		return;
		//	}

		//	Point viewPosition = e.GetPosition(Application.Current.MainWindow);
		//	Point viewPosition2 = e.GetPosition(e.OriginalSource as IInputElement);

		//	if (Keyboard.Modifiers.HasFlag(ModifierKeys.Control)
		//	    && e.LeftButton == MouseButtonState.Pressed
		//	    && !(e.OriginalSource is Thumb)) // Don't block the scrollbars.
		//	{
		//		// Move node
		//		CaptureMouse();
		//		Vector viewOffset = viewPosition - lastMousePosition2;
		//		e.Handled = true;
		//		Log.Debug($"Move {viewOffset.TS()}");
		//		viewModel.ResizeeNode(viewOffset, viewPosition2);
		//	}
		//	else
		//	{
		//		// End of move
		//		ReleaseMouseCapture();
		//	}

		//	lastMousePosition2 = viewPosition;
		//}
	}
}
