using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using Dependinator.Utils;
using Dependinator.Utils.UI.VirtualCanvas;


namespace Dependinator.Modeling.Nodes
{
	/// <summary>
	/// Interaction logic for NodesView.xaml
	/// </summary>
	public partial class NodesView : UserControl
	{
		private static readonly double ZoomSpeed = 2000.0;


		private Point lastMousePosition;
		private TouchPoint lastFirstTouchPoint;
		private TouchPoint lastSecondTouchPoint;
		private double lastLength = 0;
		private NodesViewModel viewModel;

		private List<TouchDevice> activeTouchDevices = new List<TouchDevice>();


		public NodesView()
		{
			InitializeComponent();

		}


		private void ZoomableCanvas_Loaded(object sender, RoutedEventArgs e)
		{
			viewModel = (NodesViewModel)DataContext;
			if (viewModel != null)
			{
				viewModel.SetCanvas((ZoomableCanvas)sender, this);

			}
		}


		public void SetFocus()
		{
			ItemsListBox.Focus();
		}

		private void MouseDobleClick(object sender, MouseButtonEventArgs e)
		{
		}


		private void EventMouseUp(object sender, MouseButtonEventArgs e)
		{
		}

		private void MouseEntering(object sender, MouseEventArgs e)
		{
		}


		private void MouseLeaving(object sender, MouseEventArgs e)
		{
		}



		protected override void OnMouseMove(MouseEventArgs e)
		{
			if (activeTouchDevices.Any())
			{
				// Touch is already moving, so this is a fake mouse event
				e.Handled = true;
				return;
			}

			if (!Keyboard.Modifiers.HasFlag(ModifierKeys.Control) && !viewModel.IsRoot)
			{
				// Root node move only active on root node
				return;
			}

			Point viewPosition = e.GetPosition(ItemsListBox);

			if (e.LeftButton == MouseButtonState.Pressed
				&& !(e.OriginalSource is Thumb)) // Don't block the scrollbars.
			{
				// Move canvas
				CaptureMouse();
				Vector viewOffset = viewPosition - lastMousePosition;
				e.Handled = true;
				viewModel.MoveCanvas(viewOffset);
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
			if (Keyboard.Modifiers.HasFlag(ModifierKeys.Control))
			{
				e.Handled = false;
				return;
			}

			int wheelDelta = e.Delta;
			Point viewPosition = e.GetPosition(ItemsListBox);

			double zoom = Math.Pow(2, wheelDelta / ZoomSpeed);
			viewModel.ZoomRoot(zoom, viewPosition);
			e.Handled = true;
		}


		protected override void OnTouchDown(TouchEventArgs e)
		{
			if (!viewModel.IsRoot)
			{
				return;
			}

			if (activeTouchDevices.Count > 1)
			{
				Log.Warn("No support for multi-touch yet");
				return;
			}

			activeTouchDevices.Add(e.TouchDevice);
			CaptureTouch(e.TouchDevice);

			if (activeTouchDevices.Count == 1)
			{
				lastFirstTouchPoint = e.GetTouchPoint(ItemsListBox);
			}
			else
			{
				lastSecondTouchPoint = e.GetTouchPoint(ItemsListBox);
				lastLength = (lastSecondTouchPoint.Position - lastFirstTouchPoint.Position).Length;
			}

			e.Handled = true;
		}


		protected override void OnTouchUp(TouchEventArgs e)
		{
			if (!viewModel.IsRoot)
			{
				return;
			}

			// Touch move is ending
			ReleaseTouchCapture(e.TouchDevice);
			e.Handled = true;

			activeTouchDevices.Remove(e.TouchDevice);
		}


		protected override void OnTouchMove(TouchEventArgs e)
		{
			if (!viewModel.IsRoot)
			{
				return;
			}

			TouchPoint currentPoint = e.GetTouchPoint(ItemsListBox);

			if (activeTouchDevices.Count == 1 && lastFirstTouchPoint.TouchDevice.Id == currentPoint.TouchDevice.Id)
			{
				// Touch move
				Vector offset = currentPoint.Position - lastFirstTouchPoint.Position;

				viewModel.MoveCanvas(offset);
				lastFirstTouchPoint = currentPoint;
			}
			else if (activeTouchDevices.Count == 2)
			{
				// zoom or pinch			
				if (currentPoint.TouchDevice.Id == lastFirstTouchPoint.TouchDevice.Id)
				{
					// Moved first finger
					lastFirstTouchPoint = currentPoint;
				}
				else if (currentPoint.TouchDevice.Id == lastSecondTouchPoint.TouchDevice.Id)
				{
					// Moved second finger
					lastSecondTouchPoint = currentPoint;
				}
				else
				{
					// Neither first or second finger (multi touch not yet supported
					return;
				}

				Vector vector = lastSecondTouchPoint.Position - lastFirstTouchPoint.Position;

				double currentLength = vector.Length;
				double zoomFactor = currentLength / lastLength;
				lastLength = currentLength;

				Point viewPosition = lastFirstTouchPoint.Position + (vector / 2);
				viewModel.ZoomRoot(zoomFactor, viewPosition);
			}

			e.Handled = true;
		}




		//protected override void OnPreviewMouseUp(MouseButtonEventArgs e)
		//{
		//	// Log.Debug($"Canvas offset {canvas.Offset}");

		//	if (e.ChangedButton == MouseButton.Left)
		//	{
		//		Point viewPosition = e.GetPosition(ItemsListBox);

		//		viewModel.Clicked(viewPosition);
		//	}

		//	base.OnPreviewMouseUp(e);
		//}





		//private void MouseDobleClick(object sender, MouseButtonEventArgs e)
		//{
		//	Point viewPoint = e.GetPosition(ItemsListBox);
		//	if (viewPoint.X > viewModel.GraphWidth)
		//	{
		//		viewModel.ToggleCommitDetails();
		//	}
		//}


		//private void MouseEntering(object sender, MouseEventArgs e)
		//{
		//	ListBoxItem item = sender as ListBoxItem;
		//	if (item != null)
		//	{
		//		BranchViewModel branch = item.Content as BranchViewModel;
		//		if (branch != null)
		//		{
		//			viewModel.MouseEnterBranch(branch);
		//		}

		//		CommitViewModel commit = item.Content as CommitViewModel;
		//		if (commit != null)
		//		{
		//			Point viewPoint = e.GetPosition(ItemsListBox);
		//			if (viewPoint.X < viewModel.GraphWidth)
		//			{
		//				branch = viewModel.Branches.FirstOrDefault(b => b.Branch == commit.Commit.Branch);
		//				if (branch != null)
		//				{
		//					viewModel.MouseEnterBranch(branch);
		//				}
		//			}

		//			if (viewPoint.X > viewModel.GraphWidth)
		//			{
		//				branch = viewModel.Branches.FirstOrDefault(b => b.Branch == commit.Commit.Branch);
		//				if (branch != null)
		//				{
		//					viewModel.MouseLeaveBranch(branch);
		//				}

		//			}
		//		}
		//	}
		//}


		//private void MouseLeaving(object sender, MouseEventArgs e)
		//{
		//	ListBoxItem item = sender as ListBoxItem;
		//	if (item != null)
		//	{
		//		BranchViewModel branch = item.Content as BranchViewModel;
		//		if (branch != null)
		//		{
		//			viewModel.MouseLeaveBranch(branch);
		//		}

		//		CommitViewModel commit = item.Content as CommitViewModel;
		//		if (commit != null)
		//		{
		//			Point viewPoint = e.GetPosition(ItemsListBox);
		//			if (viewPoint.X < viewModel.GraphWidth)
		//			{
		//				branch = viewModel.Branches.FirstOrDefault(b => b.Branch == commit.Commit.Branch);
		//				if (branch != null)
		//				{
		//					viewModel.MouseLeaveBranch(branch);
		//				}
		//			}
		//		}
		//	}
		//}


		//private void EventMouseUp(object sender, MouseButtonEventArgs e)
		//{
		//	ListBoxItem item = sender as ListBoxItem;
		//	if (item != null)
		//	{
		//		BranchViewModel branch = item.Content as BranchViewModel;
		//		if (branch != null)
		//		{

		//		}
		//	}
		//}
	}
}
