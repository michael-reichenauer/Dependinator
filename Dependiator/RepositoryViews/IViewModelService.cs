using System.Threading.Tasks;
using System.Windows.Media;
using Dependiator.GitModel;


namespace Dependiator.RepositoryViews
{
	internal interface IViewModelService
	{
		void UpdateViewModel(MainViewModel mainViewModel);

		int ToggleMergePoint(MainViewModel repositoryViewModel, Commit commit);

		Task SetFilterAsync(MainViewModel repositoryViewModel, string filterText);

		void ShowBranch(MainViewModel repositoryViewModel, Branch branch);

		void HideBranch(MainViewModel repositoryViewModel, Branch branch);
		Brush GetSubjectBrush(Commit commit);
	}
}