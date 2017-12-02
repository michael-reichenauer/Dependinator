using System;
using Dependinator.Common.Environment.Private;
using Dependinator.Common.MessageDialogs;
using Dependinator.Common.SettingsHandling;
using Dependinator.Utils;


namespace Dependinator
{
	/// <summary>
	/// Contains the Main method and is the entry point of the program.
	/// </summary>
	public class Program
	{
		private readonly DependencyInjection dependencyInjection = new DependencyInjection();
		private static readonly ICmd Cmd = new Cmd();


		[STAThread]
		public static void Main()
		{
			Culture.Initialize();
			Log.Init(ProgramInfo.GetLogFilePath());
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
			App application = dependencyInjection.Resolve<App>();
			ExceptionHandling.HandleDispatcherUnhandledException(); // activate after ui is started
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
			string version = AssemblyInfo.GetProgramVersion();
			string cmd = string.Join("','", Environment.GetCommandLineArgs());

			return $"Start version: {version}, cmd: '{cmd}'";
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
			string targetPath = ProgramInfo.GetInstallFilePath();
			Cmd.Start(targetPath, "");
		}


		// private static readonly string LogFileName = "Some/filePath";


		//		public static async Task MainAsync()
		//		{
		//			try
		//			{
		//				Logx.Setup(LogFileName);

		//				Log.Info("Starting AXIS Connect Camera Station Agent");

		//				DependencyInjectionx dependencyInjection = new DependencyInjectionx();

		//				using (ILifetimeScope scope = dependencyInjection.BeginLifetimeScope())
		//				{
		//					IAgentService agentService = scope.Resolve<IAgentService>();

		//					await agentService.StartAgentAsync(CancellationToken.None);

		//					await agentService.StartAgentAsync2(CancellationToken.None);
		//				}
		//			}
		//#pragma warning disable AX1011 // Catch all exceptions to be able to log them
		//			catch (Exception e)
		//			{
		//				Logx.Fatal($"Acs agent failed: {e}");
		//				throw;
		//			}
		//#pragma warning restore AX1011
		//		}

	}


	//internal interface IAgentService
	//{
	//	Task StartAgentAsync(CancellationToken ct);
	//	Task StartAgentAsync2(CancellationToken none);
	//}


	//internal interface ILifetimeScope : IDisposable
	//{
	//	T Resolve<T>();
	//}


	//internal class DependencyInjectionx
	//{
	//	public ILifetimeScope BeginLifetimeScope()
	//	{
	//		throw new NotImplementedException();
	//	}
	//}


	//internal class Logx
	//{
	//	public static void Setup(string logFileName)
	//	{
	//		throw new NotImplementedException();
	//	}


	//	public static void Info(string message)
	//	{
	//		throw new NotImplementedException();
	//	}


	//	public static void Fatal(string message)
	//	{
	//		throw new NotImplementedException();
	//	}
	//}
}
