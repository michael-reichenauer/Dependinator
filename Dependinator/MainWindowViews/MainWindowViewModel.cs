﻿using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;
using Dependinator.Common;
using Dependinator.Common.Installation;
using Dependinator.Common.MessageDialogs;
using Dependinator.Common.SettingsHandling;
using Dependinator.Common.WorkFolders;
using Dependinator.Common.WorkFolders.Private;
using Dependinator.MainWindowViews.Private;
using Dependinator.ModelViewing;
using Dependinator.Utils;
using Dependinator.Utils.UI;
using Application = System.Windows.Application;


namespace Dependinator.MainWindowViews
{
	[SingleInstance]
	internal class MainWindowViewModel : ViewModel
	{
		private readonly ILatestVersionService latestVersionService;
		private readonly IMainWindowService mainWindowService;
		private readonly MainWindowIpcService mainWindowIpcService;

		private readonly JumpListService jumpListService = new JumpListService();

		private IpcRemotingService ipcRemotingService = null;
		private readonly WorkingFolder workingFolder;

		private readonly WindowOwner owner;
		private readonly IMessage message;

		private bool isLoaded = false;


		internal MainWindowViewModel(
			WorkingFolder workingFolder,
			WindowOwner owner,
			IMessage message,
			ILatestVersionService latestVersionService,
			IMainWindowService mainWindowService,
			MainWindowIpcService mainWindowIpcService,
			ModelViewModel modelViewModel)
		{
			this.workingFolder = workingFolder;
			this.owner = owner;
			this.message = message;
			this.latestVersionService = latestVersionService;
			this.mainWindowService = mainWindowService;
			this.mainWindowIpcService = mainWindowIpcService;

			ModelViewModel = modelViewModel;

			workingFolder.OnChange += (s, e) => Notify(nameof(WorkingFolder));
			latestVersionService.OnNewVersionAvailable += (s, e) => IsNewVersionVisible = true;
			latestVersionService.StartCheckForLatestVersion();
		}


		public bool IsInFilterMode => !string.IsNullOrEmpty(SearchBox);


		public bool IsNewVersionVisible { get => Get(); set => Set(value); }

		public string WorkingFolder => workingFolder.Name;

		public string WorkingFolderPath => workingFolder.FilePath;


		public string Title => $"{workingFolder.Name} - {Product.Name}";


		public string SearchBox
		{
			get => Get();
			set
			{
				message.ShowInfo("Search is not yet implemented.");
				//Set(value).Notify(nameof(IsInFilterMode));
				//SetSearchBoxValue(value);
			}
		}


		private void SetSearchBoxValue(string text)
		{
			ModelViewModel.SetFilter(text);
		}


		public BusyIndicator Busy => BusyIndicator();

		public ModelViewModel ModelViewModel { get; }


		public string VersionText
		{
			get
			{
				Version version = ProgramInfo.GetRunningVersion();
				DateTime buildTime = ProgramInfo.BuildTime();
				string dateText = buildTime.ToString("yyyy-MM-dd\nHH:mm");
				string text = $"Version: {version.Major}.{version.Minor}\n{dateText}";
				return text;
			}
		}

		public Command RefreshCommand => AsyncCommand(ManualRefreshAsync);
		public Command RefreshLayoutCommand => AsyncCommand(ManualRefreshLayoutAsync);

		public Command OpenFileCommand => AsyncCommand(OpenFileAsync);

		public Command RunLatestVersionCommand => AsyncCommand(RunLatestVersionAsync);

		public Command FeedbackCommand => Command(Feedback);

		public Command OptionsCommand => Command(OpenOptions);

		public Command HelpCommand => Command(OpenHelp);

		public Command MinimizeCommand => Command(Minimize);

		public Command CloseCommand => Command(CloseWindow);

		public Command ExitCommand => Command(Exit);

		public Command ToggleMaximizeCommand => Command(ToggleMaximize);

		public Command EscapeCommand => Command(Escape);

		public Command ClearFilterCommand => Command(ClearFilter);

		public Command SearchCommand => Command(Search);


		public async Task FirstLoadAsync()
		{
			if (workingFolder.IsValid)
			{
				await SetWorkingFolderAsync();
			}
			else
			{
				Dispatcher.CurrentDispatcher.BeginInvoke(() => OpenFileAsync().RunInBackground());
			}

			//else
			//{
			//	isLoaded = false;

			//	if (!TryOpenFile())
			//	{
			//		Application.Current.Shutdown(0);
			//		return;
			//	}

			//	await SetWorkingFolderAsync();
			//}
		}


		public void ClosingWindow() => ModelViewModel.Close();


		private async Task OpenFileAsync()
		{
			isLoaded = false;

			if (!TryOpenFile())
			{
				isLoaded = true;
				return;
			}

			await SetWorkingFolderAsync();

			await ModelViewModel.LoadAsync();
		}


		private async Task SetWorkingFolderAsync()
		{
			await Task.Yield();

			if (ipcRemotingService != null)
			{
				ipcRemotingService.Dispose();
			}

			ipcRemotingService = new IpcRemotingService();

			string id = ProgramInfo.GetWorkingFolderId(workingFolder);
			if (ipcRemotingService.TryCreateServer(id))
			{
				ipcRemotingService.PublishService(mainWindowIpcService);
			}
			else
			{
				// Another instance for that working folder is already running, activate that.
				ipcRemotingService.CallService<MainWindowIpcService>(id, service => service.Activate(null));
				Application.Current.Shutdown(0);
				ipcRemotingService.Dispose();
				return;
			}

			jumpListService.Add(workingFolder.FilePath);

			Notify(nameof(Title));

			//await ModelViewModel.LoadAsync();
			isLoaded = true;
		}


		private Task ManualRefreshAsync()
		{
			return ModelViewModel.ManualRefreshAsync();
		}

		private Task ManualRefreshLayoutAsync()
		{
			return ModelViewModel.ManualRefreshAsync(true);
		}

		public Task AutoRemoteCheckAsync()
		{
			return ModelViewModel.AutoRemoteCheckAsync();
		}


		private void Search()
		{
			mainWindowService.SetSearchFocus();
		}


		public Task ActivateRefreshAsync()
		{
			if (!isLoaded)
			{
				return Task.CompletedTask;
			}

			return ModelViewModel.ActivateRefreshAsync();
		}


		private void Escape()
		{
			if (!string.IsNullOrWhiteSpace(SearchBox))
			{
				SearchBox = "";
			}
			else
			{
				Minimize();
			}
		}


		public int WindowWith { set { ModelViewModel.Width = value; } }


		private void Minimize()
		{
			Application.Current.MainWindow.WindowState = WindowState.Minimized;
		}


		private void ToggleMaximize()
		{
			if (Application.Current.MainWindow.WindowState == WindowState.Maximized)
			{
				Application.Current.MainWindow.WindowState = WindowState.Normal;
			}
			else
			{
				Application.Current.MainWindow.WindowState = WindowState.Maximized;
			}
		}


		private void CloseWindow()
		{
			Application.Current.Shutdown(0);
		}

		private void Exit()
		{
			Application.Current.Shutdown(0);
		}


		private async Task RunLatestVersionAsync()
		{
			bool IsStarting = await latestVersionService.StartLatestInstalledVersionAsync();

			if (IsStarting)
			{
				// Newer version is started, close this instance
				Application.Current.Shutdown(0);
			}
		}


		private void Feedback()
		{
			try
			{
				Process process = new Process();
				
				process.StartInfo.FileName = Product.FeedbackAddress;
				process.Start();
			}
			catch (Exception ex) when (ex.IsNotFatal())
			{
				Log.Error($"Failed to open feedback link {ex}");
			}
		}


		private void OpenOptions()
		{
			try
			{
				Settings.EnsureExists<Options>();
				string optionsPath = Settings.GetFilePath<Options>();

				Log.Debug($"Open {optionsPath}");
				Process.Start("notepad.exe", optionsPath);
			}
			catch (Exception e) when (e.IsNotFatal())
			{
				Log.Error($"Failed to open options {e}");
			}
		}


		private void OpenHelp()
		{
			try
			{
				Process process = new Process();
				process.StartInfo.FileName = Product.GitHubHelpAddress;
				process.Start();
			}
			catch (Exception ex) when (ex.IsNotFatal())
			{
				Log.Error($"Failed to open help link {ex}");
			}
		}

		private void ClearFilter()
		{
			if (!string.IsNullOrWhiteSpace(SearchBox))
			{
				SearchBox = "";
			}
		}


		public bool TryOpenFile()
		{
			while (true)
			{
				// Create OpenFileDialog 
				Microsoft.Win32.OpenFileDialog dlg = new Microsoft.Win32.OpenFileDialog();

				// Set filter for file extension and default file extension 
				dlg.DefaultExt = ".exe";
				dlg.Filter = "Files (*.exe, *.dll)|*.exe;*.dll|.NET libs (*.dll)|*.dll|.NET Programs (*.exe)|*.exe";
				dlg.CheckFileExists = true;
				dlg.Multiselect = false;
				dlg.Title = "Select a .NET .dll or .exe file";
				dlg.InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);

				bool? result = dlg.ShowDialog();

				// Get the selected file name and display in a TextBox 
				if (result != true)
				{
					Log.Debug("User canceled selecting a file");
					return false;
				}


				if (workingFolder.TrySetPath(dlg.FileName))
				{
					Log.Debug($"User selected valid '{dlg.FileName}' in root '{workingFolder}'");
					return true;
				}
				else
				{
					Log.Debug($"User selected an invalid working folder: {dlg.FileName}");
				}
			}
		}
	}
}