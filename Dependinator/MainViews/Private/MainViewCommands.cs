using System;


namespace Dependiator.MainViews.Private
{
	internal class MainViewCommands : IMainViewCommands
	{
		private readonly Lazy<MainViewModel> lazyMainViewModel;


		public MainViewCommands(Lazy<MainViewModel> lazyMainViewModel)
		{
			this.lazyMainViewModel = lazyMainViewModel;
		}


		private MainViewModel viewModel => lazyMainViewModel.Value;

		public void RefreshView() => viewModel.RefreshView();

		public void ShowCommitDetails() => viewModel.ShowCommitDetails();

		public void ToggleCommitDetails() => viewModel.ToggleCommitDetails();

	}
}