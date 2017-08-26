using System;
using System.Diagnostics;
using System.Reflection;
using Dependinator.ApplicationHandling;
using Dependinator.ApplicationHandling.SettingsHandling;
using Dependinator.Common.MessageDialogs;
using Dependinator.Utils;


namespace Dependinator
{
	public class Program
	{
		private readonly DependencyInjection dependencyInjection = new DependencyInjection();
		private static readonly ICmd Cmd = new Cmd();


		[STAThread]
		public static void Main()
		{
			Log.Debug(GetStartLineText());
			Program program = new Program();
			program.Run();
		}


		private void Run()
		{
			// Add handler and logging for unhandled exceptions
			ManageUnhandledExceptions();

			// Make external assemblies that Dependinator depends on available, when needed (extracted)
			ActivateExternalDependenciesResolver();

			// Activate dependency injection support
			dependencyInjection.RegisterDependencyInjectionTypes();

			// Start application
			Settings.SetWorkingFolder(dependencyInjection.Resolve<WorkingFolder>());
			App application = dependencyInjection.Resolve<App>();
			ExceptionHandling.HandleDispatcherUnhandledException();  // activate after ui is started
			application.InitializeComponent();
			application.Run();
		}


		private static void ActivateExternalDependenciesResolver()
		{
			AssemblyResolver.Activate();
			CommandLine commandLine = new CommandLine();

			if (commandLine.IsInstall || commandLine.IsUninstall)
			{
				// LibGit2 requires native git2.dll, which should not be extracted during install/uninstall
				// Since that would create a dll next to the setup file.
				AssemblyResolver.DoNotExtractLibGit2();
			}
		}


		private static string GetStartLineText()
		{
			string version = GetProgramVersion();
			string cmd = string.Join("','", Environment.GetCommandLineArgs());

			return $"Start version: {version}, cmd: '{cmd}'";
		}


		private static string GetProgramVersion()
		{
			Assembly assembly = Assembly.GetExecutingAssembly();
			FileVersionInfo fvi = FileVersionInfo.GetVersionInfo(assembly.Location);
			return fvi.FileVersion;
		}


		private static void ManageUnhandledExceptions()
		{
			ExceptionHandling.ExceptionOccurred += (s, e) => Restart();
			ExceptionHandling.ExceptionOnStartupOccurred += (s, e) =>
				Message.ShowError("Sorry, but an unexpected error just occurred");
			ExceptionHandling.HandleUnhandledException();
		}


		private static void Restart()
		{
			string targetPath = ProgramPaths.GetInstallFilePath();
			Cmd.Start(targetPath, "");
		}
	}
}