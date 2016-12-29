using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Forms;
using Dependiator.ApplicationHandling;
using Dependiator.ApplicationHandling.SettingsHandling;
using Dependiator.Common;
using Dependiator.Common.MessageDialogs;
using Dependiator.GitModel;
using Dependiator.RepositoryViews;
using Dependiator.Utils;
using Dependiator.Utils.UI;
using Application = System.Windows.Application;


namespace Dependiator.MainWindowViews
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
			MainViewModel repositoryViewModel)
		{
			this.workingFolder = workingFolder;
			this.owner = owner;
			this.message = message;
			this.latestVersionService = latestVersionService;
			this.mainWindowService = mainWindowService;
			this.mainWindowIpcService = mainWindowIpcService;

			RepositoryViewModel = repositoryViewModel;

			workingFolder.OnChange += (s, e) => Notify(nameof(WorkingFolder));
			latestVersionService.OnNewVersionAvailable += (s, e) => IsNewVersionVisible = true;
			latestVersionService.StartCheckForLatestVersion();
		}


		public bool IsInFilterMode => !string.IsNullOrEmpty(SearchBox);


		public bool IsNewVersionVisible
		{
			get { return Get(); }
			set { Set(value); }
		}

		public string WorkingFolder => workingFolder;



		public string Title => $"{workingFolder.Name} - Dependiator";


		public string SearchBox
		{
			get { return Get(); }
			set
			{
				message.ShowInfo("Search is not yet implemented.");
				//Set(value).Notify(nameof(IsInFilterMode));
				//SetSearchBoxValue(value);
			}
		}


		private void SetSearchBoxValue(string text)
		{
			RepositoryViewModel.SetFilter(text);
		}


		public BusyIndicator Busy => BusyIndicator();

		public MainViewModel RepositoryViewModel { get; }


		public string VersionText
		{
			get
			{
				Version version = ProgramPaths.GetRunningVersion();
				DateTime buildTime = ProgramPaths.BuildTime();
				string dateText = buildTime.ToString("yyyy-MM-dd\nHH:mm");
				string text = $"Version: {version.Major}.{version.Minor}\n{dateText}";
				return text;
			}
		}

		public Command RefreshCommand => AsyncCommand(ManualRefreshAsync);

		public Command SelectWorkingFolderCommand => AsyncCommand(SelectWorkingFolderAsync);

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
				isLoaded = false;

				if (!TryLetUserSelectWorkingFolder())
				{
					Application.Current.Shutdown(0);
					return;
				}

				await SetWorkingFolderAsync();
			}
		}


		private async Task SelectWorkingFolderAsync()
		{
			isLoaded = false;

			if (!TryLetUserSelectWorkingFolder())
			{
				isLoaded = true;
				return;
			}

			await SetWorkingFolderAsync();
		}


		private async Task SetWorkingFolderAsync()
		{
			if (ipcRemotingService != null)
			{
				ipcRemotingService.Dispose();
			}

			ipcRemotingService = new IpcRemotingService();

			string id = MainWindowIpcService.GetId(workingFolder);
			if (ipcRemotingService.TryCreateServer(id))
			{
				ipcRemotingService.PublishService(mainWindowIpcService);
			}
			else
			{
				// Another Dependiator instance for that working folder is already running, activate that.
				ipcRemotingService.CallService<MainWindowIpcService>(id, service => service.Activate(null));
				Application.Current.Shutdown(0);
				ipcRemotingService.Dispose();
				return;
			}

			jumpListService.Add(workingFolder);

			Notify(nameof(Title));

			await RepositoryViewModel.LoadAsync();
			isLoaded = true;
		}


		private Task ManualRefreshAsync()
		{
			return RepositoryViewModel.ManualRefreshAsync();
		}

		public Task AutoRemoteCheckAsync()
		{
			return RepositoryViewModel.AutoRemoteCheckAsync();
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

			return RepositoryViewModel.ActivateRefreshAsync();
		}


		private void Escape()
		{
			if (!string.IsNullOrWhiteSpace(SearchBox))
			{
				SearchBox = "";
				mainWindowService.SetRepositoryViewFocus();
			}
			else if (RepositoryViewModel.IsShowCommitDetails)
			{
				RepositoryViewModel.IsShowCommitDetails = false;
				mainWindowService.SetRepositoryViewFocus();
			}
			else
			{
				Minimize();
			}
		}


		public int WindowWith
		{
			set { RepositoryViewModel.Width = value; }
		}


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
				process.StartInfo.FileName = "mailto:michael.reichenauer@gmail.com&subject=Dependiator Feedback";
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
				Process process = new Process();
				string optionsName = nameof(Options);
				process.StartInfo.FileName = Path.Combine(ProgramPaths.DataFolderPath, $"{optionsName}.json");
				process.Start();
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
				process.StartInfo.FileName = "https://github.com/michael-reichenauer/Dependiator/wiki/Help";
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
				mainWindowService.SetRepositoryViewFocus();
			}
		}


		public bool TryLetUserSelectWorkingFolder()
		{
			while (true)
			{
				var dialog = new FolderBrowserDialog()
				{
					Description = "Select a working folder with a valid git repository.",
					ShowNewFolderButton = false,
					RootFolder = Environment.SpecialFolder.MyComputer
				};

				if (workingFolder.HasValue)
				{
					dialog.SelectedPath = workingFolder;
				}

				if (dialog.ShowDialog(owner.Win32Window) != DialogResult.OK)
				{
					Log.Debug("User canceled selecting a Working folder");
					return false;
				}

				if (workingFolder.TrySetPath(dialog.SelectedPath))
				{
					Log.Debug($"User selected valid '{dialog.SelectedPath}' in root '{workingFolder}'");
					return true;
				}
				else
				{
					Log.Debug($"User selected an invalid working folder: {dialog.SelectedPath}");
				}
			}
		}
	}
}