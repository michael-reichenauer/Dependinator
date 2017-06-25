using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
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
		private NodesViewModel viewModel;
		private double cumulativeDeltaX;
		private double cumulativeDeltaY;
		private double linearVelocity;


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


		//protected override void OnMouseMove(MouseEventArgs e)
		//{
		//	if (isTouchMove)
		//	{
		//		// Touch is already moving, so this is a fake mouse event
		//		e.Handled = true;
		//		return;
		//	}
		//	if (Keyboard.Modifiers.HasFlag(ModifierKeys.Control))
		//	{
		//		return;
		//	}

		//	Point viewPosition = e.GetPosition(NodesView.ItemsListBox);

		//	if (e.LeftButton == MouseButtonState.Pressed
		//	    && !(e.OriginalSource is Thumb)) // Don't block the scrollbars.
		//	{
		//		// Move canvas
		//		CaptureMouse();
		//		Vector viewOffset = viewPosition - lastMousePosition;
		//		e.Handled = viewModel.MoveCanvas(viewOffset);
		//	}
		//	else
		//	{
		//		ReleaseMouseCapture();
		//	}

		//	lastMousePosition = viewPosition;
		//}



		protected override void OnMouseMove(MouseEventArgs e)
		{
			if (!Keyboard.Modifiers.HasFlag(ModifierKeys.Control) && !viewModel.IsRoot)
			{
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
		private void OnManipulationBoundaryFeedback(object sender, ManipulationBoundaryFeedbackEventArgs e)
		{
			e.Handled = true;
		}

		private void ListBox_ManipulationStarting(object sender, ManipulationStartingEventArgs e)
		{
			e.ManipulationContainer = this;
			e.Handled = true;
		}

		const double DeltaX = 50, DeltaY = 50, LinearVelocityX = 0.04, MinimumZoom = 0.1, MaximumZoom = 10;

		private void ListBox_ManipulationDelta(object sender, ManipulationDeltaEventArgs e)
		{
			//store values of horizontal & vertical cumulative translation
			cumulativeDeltaX = e.CumulativeManipulation.Translation.X;
			cumulativeDeltaY = e.CumulativeManipulation.Translation.Y;

			//store value of linear velocity into horizontal direction  
			linearVelocity = e.Velocities.LinearVelocity.X;

			// Added from part 2. Scale part.
			// get current matrix of the element.
			Matrix borderMatrix = ((MatrixTransform)this.RenderTransform).Matrix;

			//determine if action is zoom or pinch
			var maxScale = Math.Max(e.DeltaManipulation.Scale.X, e.DeltaManipulation.Scale.Y);

			//check if not crossing minimum and maximum zoom limit
			if ((maxScale < 1 && borderMatrix.M11 * maxScale > MinimumZoom) ||
					(maxScale > 1 && borderMatrix.M11 * maxScale < MaximumZoom))
			{
				//scale to most recent change (delta) in X & Y 
				borderMatrix.ScaleAt(e.DeltaManipulation.Scale.X,
					e.DeltaManipulation.Scale.Y,
					ActualWidth / 2,
					ActualHeight / 2);

				//render new matrix
				RenderTransform = new MatrixTransform(borderMatrix);
			}
		}

		private void ListBox_ManipulationInertiaStarting(object sender, ManipulationInertiaStartingEventArgs e)
		{
			e.ExpansionBehavior = new InertiaExpansionBehavior()
			{
				InitialVelocity = e.InitialVelocities.ExpansionVelocity,
				DesiredDeceleration = 10.0 * 96.0 / 1000000.0
			};
		}

		private void ListBox_ManipulationCompleted(object sender, ManipulationCompletedEventArgs e)
		{
		}
	}
}
