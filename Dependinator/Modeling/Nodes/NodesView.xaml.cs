using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Threading;
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

		private Point initialMousePoint;
		private Point lastMousePoint;
		
		private TouchPoint initialTouchPoint1;
		private TouchPoint lastTouchPoint1;
		private TouchPoint lastTouchPoint2;
		private double lastPinchLength = 0;

		private NodesViewModel viewModel;

		private readonly Stopwatch touchClickStopWatch = new Stopwatch();
		private readonly DispatcherTimer longPressTimer;

		private readonly List<TouchDevice> activeTouchDevices = new List<TouchDevice>();


		public NodesView()
		{
			InitializeComponent();
			longPressTimer = new DispatcherTimer();
			longPressTimer.Tick += OnLongPressTime;
			longPressTimer.Interval = TimeSpan.FromMilliseconds(500);
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
				Vector viewOffset = viewPosition - lastMousePoint;
				e.Handled = true;
				viewModel.MoveCanvas(viewOffset);
			}
			else
			{
				// End of move
				ReleaseMouseCapture();
			}

			lastMousePoint = viewPosition;
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

			if (activeTouchDevices.Count == 1)
			{
				// First finger touch, check if possible click or long-press or else
				touchClickStopWatch.Restart();
				longPressTimer.Start();

				initialTouchPoint1 = e.GetTouchPoint(ItemsListBox);
				lastTouchPoint1 = e.GetTouchPoint(ItemsListBox);
			}
			else
			{
				// Second finger touch for zoom or pinch
				longPressTimer.Stop();

				lastTouchPoint2 = e.GetTouchPoint(ItemsListBox);
				lastPinchLength = (lastTouchPoint2.Position - lastTouchPoint1.Position).Length;
			}

			CaptureTouch(e.TouchDevice);
			e.Handled = true;
		}


		protected override void OnTouchUp(TouchEventArgs e)
		{
			if (!viewModel.IsRoot)
			{
				return;
			}
			
			if (activeTouchDevices.Count == 1 && lastTouchPoint1.TouchDevice.Id == e.TouchDevice.Id)
			{
				// First finger upp, checking if distance is small enough to count as click or long-press
				touchClickStopWatch.Stop();
				longPressTimer.Stop();

				TouchPoint currentPoint = e.GetTouchPoint(ItemsListBox);

				if ((currentPoint.Position - initialTouchPoint1.Position).Length < 10)
				{
					if (touchClickStopWatch.Elapsed < TimeSpan.FromMilliseconds(200))
					{
						// A one finger short click
						Log.Warn("Touch click");
					}
				}
			}

			activeTouchDevices.Remove(e.TouchDevice);
			ReleaseTouchCapture(e.TouchDevice);
			e.Handled = true;
		}


		private void OnLongPressTime(object sender, EventArgs e)
		{
			longPressTimer.Stop();

			if ((lastTouchPoint1.Position - initialTouchPoint1.Position).Length < 10)
			{
				Log.Warn("Touch long-press");
			}
		}


		protected override void OnTouchMove(TouchEventArgs e)
		{
			if (!viewModel.IsRoot)
			{
				return;
			}

			TouchPoint currentPoint = e.GetTouchPoint(ItemsListBox);

			if (activeTouchDevices.Count == 1 && lastTouchPoint1.TouchDevice.Id == currentPoint.TouchDevice.Id)
			{
				// One finger touch move
				Vector offset = currentPoint.Position - lastTouchPoint1.Position;

				viewModel.MoveCanvas(offset);
				lastTouchPoint1 = currentPoint;
			}
			else if (activeTouchDevices.Count == 2)
			{
				// Two finger touch zoom or pinch			
				if (currentPoint.TouchDevice.Id == lastTouchPoint1.TouchDevice.Id)
				{
					// Moved first finger
					lastTouchPoint1 = currentPoint;
				}
				else if (currentPoint.TouchDevice.Id == lastTouchPoint2.TouchDevice.Id)
				{
					// Moved second finger
					lastTouchPoint2 = currentPoint;
				}
				else
				{
					// Neither first or second finger (multi touch not yet supported
					return;
				}

				Vector vector = lastTouchPoint2.Position - lastTouchPoint1.Position;

				double currentLength = vector.Length;
				double zoomFactor = currentLength / lastPinchLength;
				lastPinchLength = currentLength;

				Point viewPosition = lastTouchPoint1.Position + (vector / 2);
				viewModel.ZoomRoot(zoomFactor, viewPosition);
			}

			e.Handled = true;
		}


		protected override void OnMouseDown(MouseButtonEventArgs e)
		{
			if (e.ChangedButton == MouseButton.Left)
			{
				initialMousePoint = e.GetPosition(ItemsListBox);

			}

			base.OnPreviewMouseUp(e);
		}


		protected override void OnMouseUp(MouseButtonEventArgs e)
		{
			if (e.ChangedButton == MouseButton.Left)
			{
				Point currentPoint = e.GetPosition(ItemsListBox);
				if ((currentPoint - initialMousePoint).Length < 5)
				{
					Log.Warn("Mouse click");
				}
			}

			base.OnPreviewMouseUp(e);
		}





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
