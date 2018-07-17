using System;
using System.Threading.Tasks;
using DependinatorApi;
using DependinatorApi.ApiHandling;
using DependinatorVse.Commands.Private;
using EnvDTE;
using EnvDTE80;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using SolutionEvents = Microsoft.VisualStudio.Shell.Events.SolutionEvents;
using Task = System.Threading.Tasks.Task;


namespace DependinatorVse.Api.ApiHandling.Private
{
	internal class ApiManagerService : IApiManagerService
	{
		private ApiIpcServer apiIpcServer;
		private VsExtensionApiService vsExtensionApiService;
		private AsyncPackage package;



		public  async Task InitApiServerAsync(AsyncPackage asyncPackage)
		{
			Log.Debug("Init api");
			package = asyncPackage;

			vsExtensionApiService = new VsExtensionApiService(package);

			bool isSolutionLoaded = await IsSolutionLoadedAsync(package);

			if (isSolutionLoaded)
			{
				HandleOpenSolution();
			}
			
			SolutionEvents.OnAfterOpenSolution += HandleOpenSolution;
			SolutionEvents.OnBeforeCloseSolution += HandleCloseSolution;
		}


		private static async Task<bool> IsSolutionLoadedAsync(AsyncPackage package)
		{
			await package.JoinableTaskFactory.SwitchToMainThreadAsync();

			var solService = await package.GetServiceAsync(typeof(SVsSolution)) as IVsSolution;
			if (solService == null)
			{
				return false;
			}

			ErrorHandler.ThrowOnFailure(solService.GetProperty((int)__VSPROPID.VSPROPID_IsSolutionOpen, out object value));

			return value is bool isSolOpen && isSolOpen;
		}


		private void HandleCloseSolution(object sender, EventArgs e)
		{
			Log.Warn($"Close solution");
			apiIpcServer?.Dispose();
			apiIpcServer = null;
		}


		private async void HandleOpenSolution(object sender = null, EventArgs e = null)
		{
			await package.JoinableTaskFactory.SwitchToMainThreadAsync();

			DTE2 dte = (DTE2)await package.GetServiceAsync(typeof(DTE));
			if (dte == null)
			{
				return;
			}

			Solution solution = dte.Solution;

			Register(solution.FileName);
		}


		private void Register(string solutionFilePath)
		{
			try
			{
				string serverName = GetServerName(solutionFilePath);
				Log.Debug($"Register: {serverName}");

				apiIpcServer?.Dispose();
				apiIpcServer = new ApiIpcServer(serverName);

				if (!apiIpcServer.TryPublishService<IVsExtensionApi>(vsExtensionApiService))
				{
					throw new ApplicationException($"Failed to register rpc instance {serverName}");
				}

				Log.Debug($"Registered: {serverName}");
			}
			catch (Exception e)
			{
				Log.Error($"Error {e}");
				throw;
			}
		}


		private static string GetServerName(string solutionFilePath) =>
			ApiServerNames.ExtensionApiServerName(solutionFilePath);
	}
}