using System;
using System.IO;
using System.Threading;
using System.Windows;
using Dependinator.Common.Environment;
using Dependinator.Common.Installation;
using Dependinator.Common.MessageDialogs;
using Dependinator.Common.ModelMetadataFolders.Private;
using Dependinator.Common.SettingsHandling;
using Dependinator.Common.ThemeHandling;
using Dependinator.MainWindowViews;
using Dependinator.Utils;
using Dependinator.Utils.Applications;


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
		private readonly IModelMetadataService modelMetadataService;
		private readonly IExistingInstanceService existingInstanceService;


		// This mutex is used by the installer (and uninstaller) to determine if instances are running
		private static Mutex applicationMutex;


		internal App(
			ICommandLine commandLine,
			IThemeService themeService,
			IInstaller installer,
			Lazy<MainWindow> mainWindow,
			IModelMetadataService modelMetadataService,
			IExistingInstanceService existingInstanceService)
		{
			this.commandLine = commandLine;
			this.themeService = themeService;
			this.installer = installer;
			this.mainWindow = mainWindow;
			this.modelMetadataService = modelMetadataService;
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

			if (commandLine.HasFile)
			{
				modelMetadataService.SetModelFilePath(commandLine.FilePath);
			}

			if (IsCommands())
			{
				// Command line contains some command like diff 
				// which will be handled and then this instance can end.
				HandleCommand();
				Application.Current.Shutdown(0);
				return;
			}

			if (commandLine.IsRunInstalled)
			{
				if (!existingInstanceService.WaitForOtherInstance())
				{
					// Another instance for this working folder is still running and did not close
					Application.Current.Shutdown(0);
					return;
				}
			}
			else
			{
				if (TryActivateExistingInstance())
				{
					// Another instance for this working folder is already running and it received the
					// command line from this instance, lets exit this instance, while other instance continuous
					Application.Current.Shutdown(0);
					return;
				}
			}

			Log.Usage($"Start version: {Program.Version}");
			Track.StartProgram();
			Start();
		}


		protected override void OnExit(ExitEventArgs e)
		{
			Log.Usage("Exit program");
			Track.ExitProgram();
			base.OnExit(e);
		}


		private bool TryActivateExistingInstance()
		{
			string metadataFolderPath = modelMetadataService.MetadataFolderPath;
			return existingInstanceService.TryActivateExistingInstance(
				metadataFolderPath, Environment.GetCommandLineArgs());
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
			applicationMutex = new Mutex(true, Program.Guid);

			MainWindow = mainWindow.Value;

			themeService.SetThemeWpfColors();

			MainWindow.Show();

			ProgramInfo.TryDeleteTempFiles();
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


		

		private static MessageDialog CreateTempMainWindow()
		{
			// Window used as a temp main window, when handling commands (i.e. no "real" main windows)
			return new MessageDialog(null, "", "", MessageBoxButton.OK, MessageBoxImage.Information);
		}
	}
}
