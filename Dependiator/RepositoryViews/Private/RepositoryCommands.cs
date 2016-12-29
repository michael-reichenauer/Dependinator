using System;
using Dependiator.GitModel;


namespace Dependiator.RepositoryViews.Private
{
	internal class RepositoryCommands : IRepositoryCommands
	{
		private readonly Lazy<RepositoryViewModel> lazyRepositoryViewModel;


		public RepositoryCommands(Lazy<RepositoryViewModel> lazyRepositoryViewModel)
		{
			this.lazyRepositoryViewModel = lazyRepositoryViewModel;
		}


		private RepositoryViewModel viewModel => lazyRepositoryViewModel.Value;

		public void RefreshView() => viewModel.RefreshView();

		public void ShowCommitDetails() => viewModel.ShowCommitDetails();

		public void ToggleCommitDetails() => viewModel.ToggleCommitDetails();

	}
}