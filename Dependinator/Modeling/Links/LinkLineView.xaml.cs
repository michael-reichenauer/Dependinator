using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Dependinator.Modeling.Nodes;


namespace Dependinator.Modeling.Links
{
	/// <summary>
	/// Interaction logic for LinkLineView.xaml
	/// </summary>
	public partial class LinkLineView : UserControl
	{
		private static readonly double ZoomSpeed = 3000.0;

		private bool isMouseCaptured;
		private Point capturedViewPosition;

		private LinkLineViewModel ViewModel => DataContext as LinkLineViewModel;


		public LinkLineView()
		{
			InitializeComponent();
		}

		
		private void UIElement_OnMouseDown(object sender, MouseButtonEventArgs e)
		{
			if (e.ClickCount == 2 )
			{
				ViewModel.ToggleLine();
				e.Handled = true;
			}
		}


		/// <summary>
		/// Handles "zooming" for link lines, when user used mouse wheel to zoom
		/// </summary>
		protected override void OnMouseWheel(MouseWheelEventArgs e)
		{
			if (!Keyboard.Modifiers.HasFlag(ModifierKeys.Control))
			{
				// Zooming only active when combining with Ctrl key
				return;
			}

			if (!isMouseCaptured)
			{
				// To ensure that zooming of these line works even when new lines are shown, the
				// mouse events are captured to this line until the mouse is moved, which is tracked by 
				// OnMouseMove() below.
				CaptureMouse();
				isMouseCaptured = true;
				capturedViewPosition = e.GetPosition(Application.Current.MainWindow);
			}
		
			int wheelDelta = e.Delta;
			Point viewPosition = e.GetPosition(this);
			double zoom = Math.Pow(2, wheelDelta / ZoomSpeed);
			
			ViewModel.ZoomLinks(zoom, viewPosition);			

			e.Handled = true;
		}


		protected override void OnMouseMove(MouseEventArgs e)
		{
			if (!isMouseCaptured)
			{
				return;
			}

			if (GetMovedDistance(e) > 10)
			{
				// Mouse was moved enough, consider scrolling done
				ReleaseMouseCapture();
				isMouseCaptured = false;
			}
		}


		private double GetMovedDistance(MouseEventArgs e)
		{
			Point viewPosition = e.GetPosition(Application.Current.MainWindow);
			Vector vector = viewPosition - capturedViewPosition;
			return vector.Length;
		}


		private void UIElement_OnMouseEnter(object sender, MouseEventArgs e)
		{
			ViewModel?.OnMouseEnter();
		}


		private void UIElement_OnMouseLeave(object sender, MouseEventArgs e)
		{
			ViewModel?.OnMouseLeave();
		}
	}
}
