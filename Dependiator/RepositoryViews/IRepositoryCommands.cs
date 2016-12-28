using System.Threading.Tasks;
using Dependiator.Git;
using Dependiator.GitModel;


namespace Dependiator.RepositoryViews
{
	internal interface IRepositoryCommands
	{
		void ShowCommitDetails();
		void ToggleCommitDetails();
		void ShowUncommittedDetails();

		void ShowBranch(Branch branch);
		void ShowCurrentBranch();
		void ShowDiff(Commit commit);

		Task ShowSelectedDiffAsync();

		Commit UnCommited { get; }

		void ShowBranch(BranchName branchName);

		void SetCurrentMerging(Branch branch);
		void RefreshView();
	}
}