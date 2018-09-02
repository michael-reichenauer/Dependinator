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
    ///  Contains the Main method and is the entry point of the program.
    /// </summary>
    public class Program
    {
        private readonly DependencyInjection dependencyInjection = new DependencyInjection();


        [STAThread]
        public static void Main()
        {
            Init();

            Log.Debug(GetStartLineText());

            Program program = new Program();
            program.Run();
        }


        private static void Init()
        {
            // Make external assemblies that Dependinator depends on available, when needed (extracted)
            AssemblyResolver.Activate(ProgramInfo.Assembly);

            // Set invariant culture as default to make string functions more predictable
            Culture.SetDefaultInvariantCulture();

            // Enable usage tracking (to Azure) 
            Track.Enable(
                ProgramInfo.Name,
                ProgramInfo.Version,
                ProgramInfo.IsInstalledInstance() || ProgramInfo.IsSetupFile());

            // Init trace logging to default log file
            Log.Init(ProgramInfo.GetLogFilePath());
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


        private void ManageUnhandledExceptions()
        {
            // Restart in case of unexpected errors. but in case of errors at program start
            // A message box is shown instead, in order to avoid endless restarts  
            ExceptionHandling.ExceptionOccurred += (s, e) => Restart();
            ExceptionHandling.ExceptionOnStartupOccurred += (s, e) =>
                Message.ShowError("Sorry, but an unexpected error just occurred");

            ExceptionHandling.HandleUnhandledException();
        }


        private void Restart()
        {
            // Restarts the program for the current model
            ModelMetadata modelMetadata = dependencyInjection.Resolve<ModelMetadata>();
            StartInstanceService startInstanceService = new StartInstanceService();
            startInstanceService.StartInstance(modelMetadata.ModelFilePath);
        }


        private static string GetStartLineText()
        {
            string cmd = string.Join("','", Environment.GetCommandLineArgs());
            return $"Start version: {ProgramInfo.Version}, cmd: '{cmd}'";
        }
    }
}
