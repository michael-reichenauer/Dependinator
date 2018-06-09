using System;
using System.Diagnostics;
using Dependinator.Common.SettingsHandling;
using Log = Dependinator.Utils.Log;


namespace Dependinator.MainWindowViews.Private
{
	internal class MainWindowService : IMainWindowService
	{
		private readonly ISettingsService settingsService;
		private readonly Lazy<MainWindow> mainWindow;


		public MainWindowService(
			ISettingsService settingsService,
			Lazy<MainWindow> mainWindow)
		{
			this.settingsService = settingsService;
			this.mainWindow = mainWindow;
		}


		public bool IsNewVersionAvailable { set => mainWindow.Value.IsNewVersionAvailable = value; }

		public void SetSearchFocus() => mainWindow.Value.SetSearchFocus();

		public void SetMainWindowFocus() => mainWindow.Value.Focus();


		public void SendFeedback()
		{
			try
			{
				Process process = new Process();

				process.StartInfo.FileName = ProgramInfo.FeedbackAddress;
				process.Start();
			}
			catch (Exception ex) when (ex.IsNotFatal())
			{
				Log.Error($"Failed to open feedback link {ex}");
			}
		}


		public void OpenOptions()
		{
			try
			{
				settingsService.EnsureExists<Options>();
				string optionsPath = settingsService.GetFilePath<Options>();

				Log.Debug($"Open {optionsPath}");
				Process.Start("notepad.exe", optionsPath);
			}
			catch (Exception e) when (e.IsNotFatal())
			{
				Log.Error($"Failed to open options {e}");
			}
		}


		public void OpenHelp()
		{
			try
			{
				Process process = new Process();
				process.StartInfo.FileName = ProgramInfo.GitHubHelpAddress;
				process.Start();
			}
			catch (Exception ex) when (ex.IsNotFatal())
			{
				Log.Error($"Failed to open help link {ex}");
			}
		}
	}
}