using System;
using System.IO;
using System.Threading;
using System.Windows;
using Dependinator.Common.Environment;
using Dependinator.Common.Installation;
using Dependinator.Common.MessageDialogs;
using Dependinator.Common.ModelMetadataFolders;
using Dependinator.Common.ModelMetadataFolders.Private;
using Dependinator.Common.SettingsHandling;
using Dependinator.Common.ThemeHandling;
using Dependinator.MainWindowViews;
using Dependinator.Utils;


namespace Dependinator
{
	/// <summary>
	/// Interaction logic for App.xaml.
	/// </summary>
	public partial class App : Application
	{
		private readonly ICommandLine commandLine;
		private readonly IThemeService themeService;
		private readonly IInstaller installer;
		private readonly Lazy<MainWindow> mainWindow;
		private readonly ModelMetadata modelMetadata;
		private readonly IExistingInstanceService existingInstanceService;


		// This mutex is used by the installer (and uninstaller) to determine if instances are running
		private static Mutex applicationMutex;


		internal App(
			ICommandLine commandLine,
			IThemeService themeService,
			IInstaller installer,
			Lazy<MainWindow> mainWindow,
			ModelMetadata modelMetadata,
			IExistingInstanceService existingInstanceService)
		{
			this.commandLine = commandLine;
			this.themeService = themeService;
			this.installer = installer;
			this.mainWindow = mainWindow;
			this.modelMetadata = modelMetadata;
			this.existingInstanceService = existingInstanceService;
		}


		protected override void OnStartup(StartupEventArgs e)
		{
			base.OnStartup(e);

			if (IsInstallOrUninstall())
			{
				// An installation or uninstallation was triggered, lets end this instance
				Application.Current.Shutdown(0);
				return;
			}

			if (IsCommands())
			{
				// Command line contains some command like diff 
				// which will be handled and then this instance can end.
				HandleCommand();
				Application.Current.Shutdown(0);
				return;
			}

			if (TryActivateExistingInstance())
			{
				// Another instance for this working folder is already running and it received the
				// command line from this instance, lets exit this instance, while other instance continuous
				Application.Current.Shutdown(0);
				return;
			}

			Log.Usage($"Start version: {AssemblyInfo.GetProgramVersion()}");
			Start();
		}


		protected override void OnExit(ExitEventArgs e)
		{
			Log.Usage("Exit program");
			base.OnExit(e);
		}


		private bool TryActivateExistingInstance()
		{
			return existingInstanceService.TryActivateExistingInstance(
				modelMetadata, Environment.GetCommandLineArgs());
		}


		private bool IsInstallOrUninstall()
		{
			if (commandLine.IsInstall || commandLine.IsUninstall)
			{
				// Tis is an installation (Setup file or "/install" arg) or uninstallation (/uninstall arg)
				// Need some temp main window when only message boxes will be shown for commands
				MainWindow = CreateTempMainWindow();

				installer.InstallOrUninstall();

				return true;
			}

			return false;
		}


		private void HandleCommand()
		{
			// Need some main window when only message boxes will be shown for commands
			MainWindow = CreateTempMainWindow();

			// Commands like Install, Uninstall, Diff, can be handled immediately
			HandleCommands();
		}


		private void Start()
		{
			// This mutex is used by the installer (or uninstaller) to determine if instances are running
			applicationMutex = new Mutex(true, Product.Guid);

			MainWindow = mainWindow.Value;

			themeService.SetThemeWpfColors();
			
			MainWindow.Show();

			TryDeleteTempFiles();
		}


		private bool IsCommands()
		{
			// No commands yet
			return false;
		}


		private void HandleCommands()
		{
			// No commands yet
		}

	
		private void TryDeleteTempFiles()
		{
			try
			{
				string tempFolderPath = ProgramInfo.GetTempFolderPath();
				string searchPattern = $"{ProgramInfo.TempPrefix}*";
				string[] tempFiles = Directory.GetFiles(tempFolderPath, searchPattern);
				foreach (string tempFile in tempFiles)
				{
					try
					{
						Log.Debug($"Deleting temp file {tempFile}");
						File.Delete(tempFile);
					}
					catch (Exception e)
					{
						Log.Debug($"Failed to delete temp file {tempFile}, {e.Message}. Deleting at reboot");
					}
				}
			}
			catch (Exception e)
			{
				Log.Warn($"Failed to delete temp files {e}");
			}
		}


		private static MessageDialog CreateTempMainWindow()
		{
			// Window used as a temp main window, when handling commands (i.e. no "real" main windows)
			return new MessageDialog(null, "", "", MessageBoxButton.OK, MessageBoxImage.Information);
		}



	}
}
