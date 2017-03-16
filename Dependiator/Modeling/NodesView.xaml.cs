using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Dependiator.Utils.UI.VirtualCanvas;


namespace Dependiator.Modeling
{
	/// <summary>
	/// Interaction logic for NodesView.xaml
	/// </summary>
	public partial class NodesView : UserControl
	{
		private NodesViewModel viewModel;


		public NodesView()
		{
			InitializeComponent();

		}


		private async void ZoomableCanvas_Loaded(object sender, RoutedEventArgs e)
		{
			viewModel = (NodesViewModel)DataContext;
			if (viewModel != null)
			{
				viewModel.SetCanvas((ZoomableCanvas)sender);

				await viewModel.LoadAsync();
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
