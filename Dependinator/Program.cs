using System;
using System.Reflection;
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


		public static readonly string Name = "Dependinator";
		public static readonly string Guid = "ee48e8b2-701f-4881-815f-dc7fd8139061";
		public static readonly Assembly Assembly = typeof(Program).Assembly;
		public static readonly string Version = Assembly.GetFileVersion();
		public static readonly string Location = Assembly.Location;

		public static readonly string FeedbackAddress =
			$"mailto:michael.reichenauer@gmail.com&subject={Name} Feedback";

		public static readonly string GitHubHelpAddress =
			$"https://github.com/michael-reichenauer/{Name}/wiki/Help";



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
			AssemblyResolver.Activate(Assembly);

			// Activate dependency injection support
			dependencyInjection.RegisterDependencyInjectionTypes(Assembly);

			// Start application
			App application = dependencyInjection.Resolve<App>();
			ExceptionHandling.HandleDispatcherUnhandledException(); // activate after ui is started
			application.InitializeComponent();
			application.Run();
		}


		private static string GetStartLineText()
		{
			string cmd = string.Join("','", Environment.GetCommandLineArgs());

			return $"Start version: {Version}, cmd: '{cmd}'";
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
