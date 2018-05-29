using System;
using Dependinator.Common.MessageDialogs;
using Dependinator.Common.ModelMetadataFolders;
using Dependinator.Common.ModelMetadataFolders.Private;
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
			AssemblyResolver.Activate();

			// Activate dependency injection support
			dependencyInjection.RegisterDependencyInjectionTypes();

			// Start application
			App application = dependencyInjection.Resolve<App>();
			ExceptionHandling.HandleDispatcherUnhandledException(); // activate after ui is started
			application.InitializeComponent();
			application.Run();
		}


		private static string GetStartLineText()
		{
			string version = AssemblyInfo.GetProgramVersion();
			string cmd = string.Join("','", Environment.GetCommandLineArgs());

			return $"Start version: {version}, cmd: '{cmd}'";
		}


		private void ManageUnhandledExceptions()
		{
			ExceptionHandling.ExceptionOccurred += (s, e) => Restart();
			ExceptionHandling.ExceptionOnStartupOccurred += (s, e) =>
				Message.ShowError("Sorry, but an unexpected error just occurred");

			ExceptionHandling.HandleUnhandledException();
		}


		private void Restart()
		{
			ModelMetadata modelMetadata = dependencyInjection.Resolve<ModelMetadata>();
			StartInstanceService startInstanceService = new StartInstanceService();
			startInstanceService.StartInstance(modelMetadata.ModelFilePath);
		}
	}
}
