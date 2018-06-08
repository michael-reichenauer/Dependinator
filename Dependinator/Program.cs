using System;
using Dependinator.Common.MessageDialogs;
using Dependinator.Common.ModelMetadataFolders;
using Dependinator.Common.ModelMetadataFolders.Private;
using Dependinator.Utils;
using Dependinator.Utils.Dependencies;
using Dependinator.Utils.ErrorHandling;


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
			// Make external assemblies that Dependinator depends on available, when needed (extracted)
			AssemblyResolver.Activate(ProgramInfo.Assembly);

			Culture.Initialize();
			Track.Enable(
				ProgramInfo.Name,
				ProgramInfo.Version,
				ProgramInfo.IsInstalledInstance() || ProgramInfo.IsSetupFile());
			Log.Init(ProgramInfo.GetLogFilePath());
			Log.Debug(GetStartLineText());
			Program program = new Program();
			program.Run();
		}


		private void Run()
		{
			// Add handler and logging for unhandled exceptions
			ManageUnhandledExceptions();

			// Activate dependency injection support
			dependencyInjection.RegisterTypes(ProgramInfo.Assembly);

			// Start application
			App application = dependencyInjection.Resolve<App>();
			ExceptionHandling.HandleDispatcherUnhandledException(); // activate after ui is started
			application.InitializeComponent();
			application.Run();
		}


		private static string GetStartLineText()
		{
			string cmd = string.Join("','", Environment.GetCommandLineArgs());

			return $"Start version: {ProgramInfo.Version}, cmd: '{cmd}'";
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
