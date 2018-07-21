
using DependinatorApi;
using DependinatorApi.ApiHandling;
using DependinatorVse.Commands.Private;
using EnvDTE;
using EnvDTE80;
using Microsoft.VisualStudio.Shell;


namespace DependinatorVse.Api
{
	/// <summary>
	/// Api published byt the Dependinator Visual Studio extension.
	/// Called by the Dependinator.exe, when triggering actions like Activate and ShowFile
	/// </summary>
	public class VsExtensionApiService : ApiIpcService, IVsExtensionApi
	{
		private readonly AsyncPackage package;


		public VsExtensionApiService(AsyncPackage package)
		{
			this.package = package;
		}


#pragma warning disable VSTHRD100 // Api functions does not support Task type
		public async void Activate()
#pragma warning restore VSTHRD100
		{
			await package.JoinableTaskFactory.SwitchToMainThreadAsync();

			DTE2 dte = (DTE2)await package.GetServiceAsync(typeof(DTE));
			if (dte == null) return;

			ActivateMainWindow(dte);
		}


#pragma warning disable VSTHRD100 // Api functions does not support Task type
		public async void ShowFile(string filePath, int lineNumber)
#pragma warning restore VSTHRD100 
		{
			await package.JoinableTaskFactory.SwitchToMainThreadAsync();

			Log.Debug($"Show {filePath}");
			
			DTE2 dte = (DTE2)await package.GetServiceAsync(typeof(DTE));
			if (dte == null) return;
			
			dte.ExecuteCommand("File.OpenFile", $"\"{filePath}\"");
			dte.ExecuteCommand("Edit.GoTo", $"{lineNumber}");
		}


		private static void ActivateMainWindow(DTE2 dte)
		{
			Window mainWindow = dte.MainWindow;
			vsWindowState state = mainWindow.WindowState;
			mainWindow.WindowState = vsWindowState.vsWindowStateMinimize;
			mainWindow.Activate();
			mainWindow.WindowState = state == vsWindowState.vsWindowStateMaximize 
				? vsWindowState.vsWindowStateMaximize
				: vsWindowState.vsWindowStateNormal;
		}
	}
}