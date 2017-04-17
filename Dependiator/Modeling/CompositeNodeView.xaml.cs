﻿using System;
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
		private static readonly double ZoomSpeed = 3000.0;

		private Point lastMousePosition;

		public CompositeNodeView()
		{
			InitializeComponent();
		}


		protected override void OnMouseWheel(MouseWheelEventArgs e)
		{
			//Cursors.SizeAll
			if (!Keyboard.Modifiers.HasFlag(ModifierKeys.Control))
			{
				return;
			}

			CompositeNodeViewModel viewModel = DataContext as CompositeNodeViewModel;
			if (viewModel == null)
			{
				return;
			}

			int wheelDelta = e.Delta;
			Point viewPosition = e.GetPosition(NodesView.ItemsListBox);

			if (e.OriginalSource is ListBox)
			{
				double zoom = Math.Pow(2, wheelDelta / ZoomSpeed);
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
			CompositeNodeViewModel viewModel = DataContext as CompositeNodeViewModel;
			if (viewModel == null)
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

				viewModel.MoveNode(viewOffset, viewPosition2);
			}
			else
			{
				// End of move
				ReleaseMouseCapture();
			}

			lastMousePosition = viewPosition;
		}


		private void ToolTip_OnOpened(object sender, RoutedEventArgs e)
		{
			CompositeNodeViewModel viewModel = DataContext as CompositeNodeViewModel;
			if (viewModel == null)
			{
				return;
			}

			viewModel.UpdateToolTip();

		}
	}
}
