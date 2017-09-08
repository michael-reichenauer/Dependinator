using System;
using System.Threading.Tasks;
using System.Windows;
using Dependinator.Common.Installation;
using Dependinator.Common.MessageDialogs;
using Dependinator.Common.SettingsHandling;
using Dependinator.Common.WorkFolders;
using Dependinator.Common.WorkFolders.Private;
using Dependinator.MainWindowViews.Private;
using Dependinator.ModelViewing;
using Dependinator.Utils;
using Dependinator.Utils.UI;
using Dependinator.Utils.UI.Mvvm;
using Application = System.Windows.Application;


namespace Dependinator.MainWindowViews
{
	[SingleInstance]
	internal class MainWindowViewModel : ViewModel
	{
		private readonly ILatestVersionService latestVersionService;
		private readonly IMainWindowService mainWindowService;
		private readonly MainWindowIpcService mainWindowIpcService;
		private readonly IOpenService openService;

		private readonly JumpListService jumpListService = new JumpListService();

		//private IpcRemotingService ipcRemotingService = null;
		private readonly WorkingFolder workingFolder;


		private readonly IMessage message;


		private bool isLoaded = false;


		internal MainWindowViewModel(
			WorkingFolder workingFolder,
			IMessage message,
			ILatestVersionService latestVersionService,
			IMainWindowService mainWindowService,
			MainWindowIpcService mainWindowIpcService,
			ModelViewModel modelViewModel,
			IOpenService openService)
		{
			this.workingFolder = workingFolder;
			this.message = message;

			this.latestVersionService = latestVersionService;
			this.mainWindowService = mainWindowService;
			this.mainWindowIpcService = mainWindowIpcService;
			this.openService = openService;

			ModelViewModel = modelViewModel;

			workingFolder.OnChange += (s, e) => Notify(nameof(WorkingFolder));
			latestVersionService.OnNewVersionAvailable += (s, e) => IsNewVersionVisible = true;
			latestVersionService.StartCheckForLatestVersion();
		}

		public int WindowWith { set => ModelViewModel.Width = value; }

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
				// ModelViewModel.SetFilter(value);
			}
		}



		public BusyIndicator Busy => BusyIndicator();

		public ModelViewModel ModelViewModel { get; }


		public string VersionText
		{
			get
			{
				Version version = ProgramInfo.GetCurrentInstanceVersion();
				DateTime buildTime = ProgramInfo.GetCurrentInstanceBuildTime();
				string dateText = buildTime.ToString("yyyy-MM-dd\nHH:mm");
				string text = $"Version: {version.Major}.{version.Minor}\n{dateText}";
				return text;
			}
		}

		public Command RefreshCommand => AsyncCommand(ManualRefreshAsync);
		public Command RefreshLayoutCommand => AsyncCommand(ManualRefreshLayoutAsync);

		public Command OpenFileCommand => AsyncCommand(openService.OpenFileAsync);

		public Command RunLatestVersionCommand => AsyncCommand(RunLatestVersionAsync);

		public Command FeedbackCommand => Command(mainWindowService.SendFeedback);

		public Command OptionsCommand => Command(mainWindowService.OpenOptions);

		public Command HelpCommand => Command(mainWindowService.OpenHelp);

		public Command MinimizeCommand => Command(Minimize);

		public Command CloseCommand => Command(CloseWindow);

		public Command ExitCommand => Command(Exit);

		public Command ToggleMaximizeCommand => Command(ToggleMaximize);

		public Command EscapeCommand => Command(Escape);

		public Command ClearFilterCommand => Command(ClearFilter);

		public Command SearchCommand => Command(Search);


		public async Task LoadAsync()
		{
			await Task.Yield();

			//if (workingFolder.IsValid)
			//{
			//	//await SetWorkingFolderAsync();
			//}
			//else
			//{
			//	//Dispatcher.CurrentDispatcher.BeginInvoke(() =>
			//	//{
			//	//	OpenFileDialog dialog = new OpenFileDialog(owner);
			//	//	dialog.ShowDialog();
			//	//});

			//}
			//else
			//{
			//	Dispatcher.CurrentDispatcher.BeginInvoke(() => OpenFileAsync().RunInBackground());
			//}

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

		private Task ManualRefreshAsync() => ModelViewModel.ManualRefreshAsync();

		private Task ManualRefreshLayoutAsync() => ModelViewModel.ManualRefreshAsync(true);

		private void Search() => mainWindowService.SetSearchFocus();


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



		private static void Minimize() =>
			Application.Current.MainWindow.WindowState = WindowState.Minimized;


		private static void ToggleMaximize()
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


		private static void CloseWindow() => Application.Current.Shutdown(0);

		private static void Exit() => Application.Current.Shutdown(0);


		private async Task RunLatestVersionAsync()
		{
			bool IsStarting = await latestVersionService.StartLatestInstalledVersionAsync();

			if (IsStarting)
			{
				// Newer version is started, close this instance
				Application.Current.Shutdown(0);
			}
		}



		private void ClearFilter()
		{
			if (!string.IsNullOrWhiteSpace(SearchBox))
			{
				SearchBox = "";
			}
		}
	}
}