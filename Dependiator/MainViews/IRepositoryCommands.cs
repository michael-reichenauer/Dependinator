using Dependiator.GitModel;


namespace Dependiator.MainViews
{
	internal interface IRepositoryCommands
	{
		void ShowCommitDetails();
		void ToggleCommitDetails();
		
		void RefreshView();
	}
}