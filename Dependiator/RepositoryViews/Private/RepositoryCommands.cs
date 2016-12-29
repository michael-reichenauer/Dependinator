using System;
using Dependiator.GitModel;


namespace Dependiator.RepositoryViews.Private
{
	internal class RepositoryCommands : IRepositoryCommands
	{
		private readonly Lazy<MainViewModel> lazyRepositoryViewModel;


		public RepositoryCommands(Lazy<MainViewModel> lazyRepositoryViewModel)
		{
			this.lazyRepositoryViewModel = lazyRepositoryViewModel;
		}


		private MainViewModel viewModel => lazyRepositoryViewModel.Value;

		public void RefreshView() => viewModel.RefreshView();

		public void ShowCommitDetails() => viewModel.ShowCommitDetails();

		public void ToggleCommitDetails() => viewModel.ToggleCommitDetails();

	}
}