using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;
using Dependinator.Utils;
using Dependinator.Utils.UI;
using Dependinator.Utils.UI.VirtualCanvas;


namespace Dependinator.ModelViewing.Private.Items
{
	/// <summary>
	/// Interaction logic for ItemsView.xaml
	/// </summary>
	public partial class ItemsView : UserControl
	{
		private TouchPoint initialTouchPoint1;
		private TouchPoint lastTouchPoint1;
		private TouchPoint lastTouchPoint2;
		private double lastPinchLength = 0;

		private ItemsViewModel viewModel;

		private readonly Stopwatch touchClickStopWatch = new Stopwatch();
		private readonly DispatcherTimer longPressTimer;

		private readonly List<TouchDevice> activeTouchDevices = new List<TouchDevice>();


		public ItemsView()
		{
			InitializeComponent();
			longPressTimer = new DispatcherTimer();
			longPressTimer.Tick += OnLongPressTime;
			longPressTimer.Interval = TimeSpan.FromMilliseconds(500);
		}


		protected override void OnMouseMove(MouseEventArgs e) =>
			MouseHelper.OnLeftButtonMove(this, e, (s, offset, p1, p2) => viewModel?.MoveAllItems(p1, p2));


		private void ZoomableCanvas_Loaded(object sender, RoutedEventArgs e)
		{
			viewModel = (ItemsViewModel)DataContext;
			viewModel?.SetZoomableCanvas((ZoomableCanvas)sender);
		}


		public void SetFocus()
		{
			ItemsListBox.Focus();
		}



		protected override void OnPreviewMouseWheel(MouseWheelEventArgs e)
		{
			if (viewModel.ItemsCanvas.IsFocused)
			{
				viewModel.ItemsCanvas.ZoomNode(e);
			}
		}



		protected override void OnTouchDown(TouchEventArgs e)
		{
			if (!(viewModel?.IsRoot ?? false))
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
			//if (!viewModel.IsRoot)
			//{
			//	return;
			//}

			//TouchPoint currentPoint = e.GetTouchPoint(ItemsListBox);

			//if (activeTouchDevices.Count == 1 && lastTouchPoint1.TouchDevice.Id == currentPoint.TouchDevice.Id)
			//{
			//	// One finger touch move
			//	Vector offset = currentPoint.Position - lastTouchPoint1.Position;

			//	viewModel.MoveCanvas(offset);
			//	lastTouchPoint1 = currentPoint;
			//}
			//else if (activeTouchDevices.Count == 2)
			//{
			//	// Two finger touch zoom or pinch			
			//	if (currentPoint.TouchDevice.Id == lastTouchPoint1.TouchDevice.Id)
			//	{
			//		// Moved first finger
			//		lastTouchPoint1 = currentPoint;
			//	}
			//	else if (currentPoint.TouchDevice.Id == lastTouchPoint2.TouchDevice.Id)
			//	{
			//		// Moved second finger
			//		lastTouchPoint2 = currentPoint;
			//	}
			//	else
			//	{
			//		// Neither first or second finger (multi touch not yet supported
			//		return;
			//	}

			//	Vector vector = lastTouchPoint2.Position - lastTouchPoint1.Position;

			//	double currentLength = vector.Length;
			//	double zoomFactor = currentLength / lastPinchLength;
			//	lastPinchLength = currentLength;

			//	Point viewPosition = lastTouchPoint1.Position + (vector / 2);
			//	//viewModel.ZoomRoot(zoomFactor, viewPosition);
			//}

			e.Handled = true;
		}
	}
}
