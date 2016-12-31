using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using Dependiator.Utils;
using Dependiator.Utils.UI.VirtualCanvas;
using UserControl = System.Windows.Controls.UserControl;


namespace Dependiator.MainViews
{
	/// <summary>
	/// Interaction logic for MainView.xaml
	/// </summary>
	public partial class MainView : UserControl
	{
		private MainViewModel viewModel;

		private System.Windows.Point lastMousePosition;

		public MainView()
		{
			InitializeComponent();
		}


		private void ZoomableCanvas_Loaded(object sender, RoutedEventArgs e)
		{
			viewModel = (MainViewModel)DataContext;
			viewModel.Canvas = (ZoomableCanvas)sender;
			viewModel.ListBox = ItemsListBox;

			ItemsListBox.Focus();
		}


		protected override void OnPreviewMouseWheel(MouseWheelEventArgs e)
		{
			int zoomDelta = e.Delta;
			Point currentPosition = e.GetPosition(ItemsListBox);

			e.Handled = viewModel.HandleZoom(zoomDelta, currentPosition);		
		}



		protected override void OnPreviewMouseMove(MouseEventArgs e)
		{
			System.Windows.Point position = e.GetPosition(ItemsListBox);
			ZoomableCanvas canvas = viewModel.Canvas;

			if (e.LeftButton == MouseButtonState.Pressed && position.Y < 0)
			{
				ReleaseMouseCapture();
				return;
			}

			if (e.LeftButton == MouseButtonState.Pressed
					&& !(e.OriginalSource is Thumb)) // Don't block the scrollbars.
			{
				Log.Debug($"Mouse {position}");
				CaptureMouse();
				canvas.Offset -= position - lastMousePosition;
				e.Handled = true;
			}
			else
			{
				ReleaseMouseCapture();
			}

			lastMousePosition = position;
		}



		protected override void OnPreviewMouseUp(MouseButtonEventArgs e)
		{
			// Log.Debug($"Canvas offset {canvas.Offset}");

			if (e.ChangedButton == MouseButton.Left)
			{
				Point viewPoint = e.GetPosition(ItemsListBox);

				Point position = new Point(viewPoint.X + viewModel.Canvas.Offset.X, viewPoint.Y + viewModel.Canvas.Offset.Y);

				viewModel.Clicked(position);
			}

			base.OnPreviewMouseUp(e);
		}


		private void MouseDobleClick(object sender, MouseButtonEventArgs e)
		{
			//Point viewPoint = e.GetPosition(ItemsListBox);
			//if (viewPoint.X > viewModel.GraphWidth)
			//{
			//	viewModel.ToggleCommitDetails();
			//}
		}


		private void MouseEntering(object sender, MouseEventArgs e)
		{
			//ListBoxItem item = sender as ListBoxItem;
			//if (item != null)
			//{
			//	BranchViewModel branch = item.Content as BranchViewModel;
			//	if (branch != null)
			//	{
			//		viewModel.MouseEnterBranch(branch);
			//	}

			//	CommitViewModel commit = item.Content as CommitViewModel;
			//	if (commit != null)
			//	{
			//		Point viewPoint = e.GetPosition(ItemsListBox);
			//		if (viewPoint.X < viewModel.GraphWidth)
			//		{
			//			branch = viewModel.Branches.FirstOrDefault(b => b.Branch == commit.Commit.Branch);
			//			if (branch != null)
			//			{
			//				viewModel.MouseEnterBranch(branch);
			//			}
			//		}

			//		if (viewPoint.X > viewModel.GraphWidth)
			//		{
			//			branch = viewModel.Branches.FirstOrDefault(b => b.Branch == commit.Commit.Branch);
			//			if (branch != null)
			//			{
			//				viewModel.MouseLeaveBranch(branch);
			//			}

			//		}
			//	}
			//}
		}


		private void MouseLeaving(object sender, MouseEventArgs e)
		{
			//ListBoxItem item = sender as ListBoxItem;
			//if (item != null)
			//{
			//	BranchViewModel branch = item.Content as BranchViewModel;
			//	if (branch != null)
			//	{
			//		viewModel.MouseLeaveBranch(branch);
			//	}

			//	CommitViewModel commit = item.Content as CommitViewModel;
			//	if (commit != null)
			//	{
			//		Point viewPoint = e.GetPosition(ItemsListBox);
			//		if (viewPoint.X < viewModel.GraphWidth)
			//		{
			//			branch = viewModel.Branches.FirstOrDefault(b => b.Branch == commit.Commit.Branch);
			//			if (branch != null)
			//			{
			//				viewModel.MouseLeaveBranch(branch);
			//			}
			//		}
			//	}
			//}
		}


		private void EventMouseUp(object sender, MouseButtonEventArgs e)
		{
			ListBoxItem item = sender as ListBoxItem;
			//if (item != null)
			//{
			//	BranchViewModel branch = item.Content as BranchViewModel;
			//	if (branch != null)
			//	{

			//	}
			//}
		}
	}
}
