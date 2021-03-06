﻿using System;
using System.Threading;
using System.Windows;
using Dependinator.Common.Environment;
using Dependinator.Common.Installation;
using Dependinator.Common.MessageDialogs;
using Dependinator.Common.ModelMetadataFolders.Private;
using Dependinator.Common.ThemeHandling;
using Dependinator.MainWindowViews;
using Dependinator.Utils;


namespace Dependinator
{
    /// <summary>
    ///     Interaction logic for App.xaml.
    /// </summary>
    public partial class App : Application
    {
        // This mutex is used by the installer (and uninstaller) to determine if instances are running
        private static Mutex applicationMutex;

        private readonly ICommandLine commandLine;
        private readonly IExistingInstanceService existingInstanceService;
        private readonly IInstaller installer;
        private readonly ILatestVersionService latestVersionService;
        private readonly Lazy<MainWindow> mainWindow;
        private readonly IModelMetadataService modelMetadataService;
        private readonly IThemeService themeService;


        internal App(
            ICommandLine commandLine,
            IThemeService themeService,
            IInstaller installer,
            Lazy<MainWindow> mainWindow,
            IModelMetadataService modelMetadataService,
            IExistingInstanceService existingInstanceService,
            ILatestVersionService latestVersionService)
        {
            this.commandLine = commandLine;
            this.themeService = themeService;
            this.installer = installer;
            this.mainWindow = mainWindow;
            this.modelMetadataService = modelMetadataService;
            this.existingInstanceService = existingInstanceService;
            this.latestVersionService = latestVersionService;
        }


        protected override async void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            if (IsInstallOrUninstall())
            {
                // An installation or uninstallation was triggered, lets end this instance
                Current.Shutdown(0);
                return;
            }

            if (commandLine.IsCheckUpdate)
            {
                await latestVersionService.CheckLatestVersionAsync();
                Current.Shutdown(0);
                return;
            }

            if (commandLine.HasFile)
            {
                modelMetadataService.SetModelFilePath(commandLine.FilePath);
            }


            if (commandLine.IsRunInstalled)
            {
                if (!existingInstanceService.WaitForOtherInstance())
                {
                    // Another instance for this working folder is still running and did not close
                    Current.Shutdown(0);
                    return;
                }
            }
            else
            {
                if (TryActivateExistingInstance())
                {
                    // Another instance for this working folder is already running and it received the
                    // command line from this instance, lets exit this instance, while other instance continuous
                    Current.Shutdown(0);
                    return;
                }
            }

            Log.Usage($"Start version: {ProgramInfo.Version}");
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
            return existingInstanceService.TryActivateExistingInstance(Environment.GetCommandLineArgs());
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


        private void Start()
        {
            // This mutex is used by the installer (or uninstaller) to determine if instances are running
            applicationMutex = new Mutex(true, ProgramInfo.Guid);

            MainWindow = mainWindow.Value;

            themeService.SetThemeWpfColors();

            MainWindow.Show();

            ProgramInfo.TryDeleteTempFiles();
        }


        private static MessageDialog CreateTempMainWindow()
        {
            // Window used as a temp main window, when handling commands (i.e. no "real" main windows)
            return new MessageDialog(null, "", "", MessageBoxButton.OK, MessageBoxImage.Information);
        }
    }
}
