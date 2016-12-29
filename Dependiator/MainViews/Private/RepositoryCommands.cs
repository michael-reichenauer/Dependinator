using System;


namespace Dependiator.MainViews.Private
{
	internal class RepositoryCommands : IRepositoryCommands
	{
		private readonly Lazy<MainViewModel> lazyMainViewModel;


		public RepositoryCommands(Lazy<MainViewModel> lazyMainViewModel)
		{
			this.lazyMainViewModel = lazyMainViewModel;
		}


		private MainViewModel viewModel => lazyMainViewModel.Value;

		public void RefreshView() => viewModel.RefreshView();

		public void ShowCommitDetails() => viewModel.ShowCommitDetails();

		public void ToggleCommitDetails() => viewModel.ToggleCommitDetails();

	}
}