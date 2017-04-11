﻿using System;
using System.Windows;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using UserControl = System.Windows.Controls.UserControl;


namespace Dependiator.MainViews
{
	/// <summary>
	/// Interaction logic for MainView.xaml
	/// </summary>
	public partial class MainView : UserControl
	{
		private static readonly double ZoomSpeed = 1000.0;

		private MainViewModel viewModel;

		private Point lastMousePosition;
		//private object movingObject = null;


		public MainView()
		{
			InitializeComponent();
		}


		private async void MainView_OnLoaded(object sender, RoutedEventArgs e)
		{
			viewModel = (MainViewModel)DataContext;
			NodesView.SetFocus();
			await viewModel.LoadAsync();
		}



		protected override void OnPreviewMouseWheel(MouseWheelEventArgs e)
		{
			if (Keyboard.Modifiers.HasFlag(ModifierKeys.Control))
			{
				e.Handled = false;
				return;
			}

			int wheelDelta = e.Delta;
			Point viewPosition = e.GetPosition(NodesView.ItemsListBox);

			double zoom = Math.Pow(2, wheelDelta / ZoomSpeed);
			viewModel.Zoom(zoom, viewPosition);
			e.Handled = true;
		}


		protected override void OnPreviewMouseMove(MouseEventArgs e)
		{
			Point viewPosition = e.GetPosition(NodesView.ItemsListBox);
			
			if (!Keyboard.Modifiers.HasFlag(ModifierKeys.Control)
				&& e.LeftButton == MouseButtonState.Pressed
				&& !(e.OriginalSource is Thumb)) // Don't block the scrollbars.
			{
				// Move canvas
				CaptureMouse();
				Vector viewOffset = viewPosition - lastMousePosition;
				e.Handled = viewModel.MoveCanvas(viewOffset);
			}
			//else if (Keyboard.Modifiers.HasFlag(ModifierKeys.Control)
			// && e.LeftButton == MouseButtonState.Pressed)
			//{
			//	// Move node
			//	CaptureMouse();
			//	Vector viewOffset = viewPosition - lastMousePosition;

			//	movingObject = viewModel.Move(viewPosition, viewOffset, movingObject);

			//	e.Handled = movingObject != null;
			//}
			else
			{
				// End of move
				//movingObject = null;
				ReleaseMouseCapture();
			}

			lastMousePosition = viewPosition;
		}


		protected override void OnPreviewMouseUp(MouseButtonEventArgs e)
		{
			// Log.Debug($"Canvas offset {canvas.Offset}");

			if (e.ChangedButton == MouseButton.Left)
			{
				Point viewPosition = e.GetPosition(NodesView.ItemsListBox);

				viewModel.Clicked(viewPosition);
			}

			base.OnPreviewMouseUp(e);
		}
	}
}
