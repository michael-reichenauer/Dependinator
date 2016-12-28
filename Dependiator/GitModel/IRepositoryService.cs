using System;
using System.Threading.Tasks;


namespace Dependiator.GitModel
{
	internal interface IRepositoryService
	{
		Repository Repository { get; }

		bool IsPaused { get;}

		event EventHandler<RepositoryUpdatedEventArgs> RepositoryUpdated;

		event EventHandler<RepositoryErrorEventArgs> RepositoryErrorChanged;

		bool IsRepositoryCached(string workingFolder);

		Task LoadRepositoryAsync(string workingFolder);

		Task GetFreshRepositoryAsync();

		Task CheckLocalRepositoryAsync();

		Task CheckBranchTipCommitsAsync();

		Task UpdateRepositoryAfterCommandAsync();

		Task RefreshAfterCommandAsync(bool useFreshRepository);
		Task CheckRemoteChangesAsync(bool b);

		Task GetRemoteAndFreshRepositoryAsync();
	}
}