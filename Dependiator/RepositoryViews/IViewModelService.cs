using System.Threading.Tasks;
using System.Windows.Media;
using Dependiator.GitModel;


namespace Dependiator.RepositoryViews
{
	internal interface IViewModelService
	{
		void UpdateViewModel(MainViewModel mainViewModel);

		int ToggleMergePoint(MainViewModel maninViewModel, Commit commit);

		Task SetFilterAsync(MainViewModel maninViewModel, string filterText);

		void ShowBranch(MainViewModel maninViewModel, Branch branch);

		void HideBranch(MainViewModel maninViewModel, Branch branch);
		Brush GetSubjectBrush(Commit commit);
	}
}