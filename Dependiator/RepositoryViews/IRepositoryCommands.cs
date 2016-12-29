using Dependiator.GitModel;


namespace Dependiator.RepositoryViews
{
	internal interface IRepositoryCommands
	{
		void ShowCommitDetails();
		void ToggleCommitDetails();
		
		void SetCurrentMerging(Branch branch);
		void RefreshView();
	}
}