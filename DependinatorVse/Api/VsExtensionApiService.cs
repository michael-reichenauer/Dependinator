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


		public async void ShowFile(string filePath)
		{
			await package.JoinableTaskFactory.SwitchToMainThreadAsync();

			DTE2 dte = (DTE2)await package.GetServiceAsync(typeof(DTE));
			if (dte == null)
			{
				return;
			}

			//dte.OpenFile(Constants.vsViewKindCode, filePath);
			dte.ExecuteCommand("File.OpenFile", $"\"{filePath}\"");

			Log.Debug($"Show {filePath}");
		}
	}
}