using System;
using System.ComponentModel.Design;
using System.Globalization;
using System.Linq;
using System.Threading;
using DependinatorApi;
using DependinatorApi.ApiHandling;
using DependinatorVse.Commands.Private;
using EnvDTE;
using EnvDTE80;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using Microsoft.VisualStudio.Threading;
using Task = System.Threading.Tasks.Task;

namespace DependinatorVse.Commands
{
	/// <summary>
	/// Command handler
	/// </summary>
	internal sealed class ShowInDependinatorCommand
	{
		public const int CommandId = 0x0100;
		public static readonly Guid CommandSet = new Guid("3503f324-db31-4488-9db2-c484cbd1609c");

		private readonly AsyncPackage package;


		private ShowInDependinatorCommand(AsyncPackage package, OleMenuCommandService commandService)
		{
			this.package = package ?? throw new ArgumentNullException(nameof(package));
			commandService = commandService ?? throw new ArgumentNullException(nameof(commandService));

			var menuCommandID = new CommandID(CommandSet, CommandId);
			var menuItem = new MenuCommand(this.Execute, menuCommandID);
			commandService.AddCommand(menuItem);
		}

		public static ShowInDependinatorCommand Instance { get; private set; }


		private Microsoft.VisualStudio.Shell.IAsyncServiceProvider ServiceProvider => this.package;


		public static async Task InitializeAsync(AsyncPackage package)
		{
			// Verify the current thread is the UI thread - the call to AddCommand in ShowInDependinatorCommand's constructor requires
			// the UI thread.
			await package.JoinableTaskFactory.SwitchToMainThreadAsync(CancellationToken.None);

			OleMenuCommandService commandService = await package.GetServiceAsync((typeof(IMenuCommandService))) as OleMenuCommandService;
			Instance = new ShowInDependinatorCommand(package, commandService);
		}


		/// <summary>
		/// This function is the callback used to execute the command when the menu item is clicked.
		/// See the constructor to see how the menu item is associated with this function using
		/// OleMenuCommandService service and MenuCommand class.
		/// </summary>
		private async void Execute(object sender, EventArgs e)
		{
			await package.JoinableTaskFactory.SwitchToMainThreadAsync(CancellationToken.None);

			DTE2 dte = (DTE2)await ServiceProvider.GetServiceAsync(typeof(DTE));
			if (dte == null)
			{
				return;
			}

			// When running in special developer studio, use developer Dependinator.exe 
			bool isDeveloperStudio = dte.RegistryRoot?.Contains("15.0_ae5cc26aExp") ?? false;

			Solution solution = dte.Solution;

			Document document = dte.ActiveDocument;


			//Array projects = (Array)dte.ActiveSolutionProjects;
			//Project project = projects.Cast<Project>().ElementAtOrDefault(0);

			DependinatorApiClient apiClient = new DependinatorApiClient(solution.FileName, isDeveloperStudio);

			await apiClient.ShowFileAsync(document.FullName);

		
	

			//string message = string.Format(CultureInfo.CurrentCulture, "Inside {0}.MenuItemCallback()", this.GetType().FullName);
			//string title = "ShowInDependinatorCommand";

			//// Show a message box to prove we were here
			//VsShellUtilities.ShowMessageBox(
			//		this.package,
			//		message,
			//		title,
			//		OLEMSGICON.OLEMSGICON_INFO,
			//		OLEMSGBUTTON.OLEMSGBUTTON_OK,
			//		OLEMSGDEFBUTTON.OLEMSGDEFBUTTON_FIRST);
		}
	}
}
