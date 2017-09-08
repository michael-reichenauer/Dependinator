namespace Dependinator.MainWindowViews
{
	public interface IMainWindowService
	{
		bool IsNewVersionAvailable { set; }
		void SetSearchFocus();
		void SetMainWindowFocus();
		void SendFeedback();
		void OpenOptions();
		void OpenHelp();
	}
}