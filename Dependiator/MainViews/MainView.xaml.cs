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

		private Point lastMousePosition;


		public MainView()
		{
			InitializeComponent();
		}


		private async void ZoomableCanvas_Loaded(object sender, RoutedEventArgs e)
		{
			viewModel = (MainViewModel)DataContext;
			viewModel.SetCanvas((ZoomableCanvas)sender);

			ItemsListBox.Focus();

			await viewModel.LoadAsync();
		}


		protected override void OnPreviewMouseWheel(MouseWheelEventArgs e)
		{
			int zoomDelta = e.Delta;
			Point viewPosition = e.GetPosition(ItemsListBox);

			e.Handled = viewModel.ZoomCanvas(zoomDelta, viewPosition);		
		}


		private object movingObject = null;

		

		protected override void OnPreviewMouseMove(MouseEventArgs e)
		{
			Point viewPosition = e.GetPosition(ItemsListBox);
			
			if (!Keyboard.Modifiers.HasFlag(ModifierKeys.Control)
				&& e.LeftButton == MouseButtonState.Pressed
				&& !(e.OriginalSource is Thumb)) // Don't block the scrollbars.
			{
				CaptureMouse();
				Vector viewOffset = viewPosition - lastMousePosition;
				e.Handled = viewModel.MoveCanvas(viewOffset);
			}
			else if (Keyboard.Modifiers.HasFlag(ModifierKeys.Control)
			 && e.LeftButton == MouseButtonState.Pressed)
			{
				CaptureMouse();
				Vector viewOffset = viewPosition - lastMousePosition;

				movingObject = viewModel.MoveNode(viewPosition, viewOffset, movingObject);

				e.Handled = movingObject != null;
			}
			else
			{
				movingObject = null;
				ReleaseMouseCapture();
			}

			lastMousePosition = viewPosition;
		}


		protected override void OnPreviewMouseUp(MouseButtonEventArgs e)
		{
			// Log.Debug($"Canvas offset {canvas.Offset}");

			if (e.ChangedButton == MouseButton.Left)
			{
				Point viewPosition = e.GetPosition(ItemsListBox);

				viewModel.Clicked(viewPosition);
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
