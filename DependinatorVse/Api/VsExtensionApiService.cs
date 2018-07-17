using DependinatorApi;
using DependinatorApi.ApiHandling;
using DependinatorVse.Commands.Private;
using EnvDTE;
using EnvDTE80;
using Microsoft.VisualStudio.Shell;


namespace DependinatorVse.Api
{
	public class VsExtensionApiService : ApiIpcService, IVsExtensionApi
	{
		private readonly AsyncPackage package;


		public VsExtensionApiService(AsyncPackage package)
		{
			this.package = package;
		}


#pragma warning disable VSTHRD100 // Api functions does not support Task type
		public async void ShowFile(string filePath)
#pragma warning restore VSTHRD100 
		{
			Log.Debug($"Show {filePath}");

			await package.JoinableTaskFactory.SwitchToMainThreadAsync();

			DTE2 dte = (DTE2)await package.GetServiceAsync(typeof(DTE));

			//dte.OpenFile(Constants.vsViewKindCode, filePath);
			dte?.ExecuteCommand("File.OpenFile", $"\"{filePath}\"");
		}
	}
}