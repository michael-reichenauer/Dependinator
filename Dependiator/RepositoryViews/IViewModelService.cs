using System.Threading.Tasks;
using System.Windows.Media;
using Dependiator.GitModel;


namespace Dependiator.RepositoryViews
{
	internal interface IViewModelService
	{
		void UpdateViewModel(RepositoryViewModel repositoryViewModel);

		int ToggleMergePoint(RepositoryViewModel repositoryViewModel, Commit commit);

		Task SetFilterAsync(RepositoryViewModel repositoryViewModel, string filterText);

		void ShowBranch(RepositoryViewModel repositoryViewModel, Branch branch);

		void HideBranch(RepositoryViewModel repositoryViewModel, Branch branch);
		Brush GetSubjectBrush(Commit commit);
	}
}