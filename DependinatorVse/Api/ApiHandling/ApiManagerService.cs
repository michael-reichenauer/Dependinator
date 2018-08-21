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


namespace DependinatorVse.Api.ApiHandling
{
    internal class ApiManagerService
    {
        private ApiIpcServer apiIpcServer;
        private AsyncPackage package;
        private VsExtensionApiService vsExtensionApiService;


        public async Task InitApiServerAsync(AsyncPackage asyncPackage)
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

            if (!(await package.GetServiceAsync(typeof(SVsSolution)) is IVsSolution solService))
            {
                return false;
            }

            ErrorHandler.ThrowOnFailure(solService.GetProperty((int)__VSPROPID.VSPROPID_IsSolutionOpen,
                out object value));

            return value is bool isSolOpen && isSolOpen;
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

            await RegisterAsync(solution.FileName);
        }


        private void HandleCloseSolution(object sender, EventArgs e)
        {
            Log.Warn("Close solution");
            apiIpcServer?.Dispose();
            apiIpcServer = null;
        }


        private async Task RegisterAsync(string solutionFilePath)
        {
            try
            {
                string serverName = GetServerName(solutionFilePath);
                Log.Debug($"Register: {serverName}");

                apiIpcServer?.Dispose();
                apiIpcServer = new ApiIpcServer(serverName);

                await apiIpcServer.PublishServiceAsync<IVsExtensionApi>(vsExtensionApiService);

                Log.Debug($"Registered: {serverName}");
            }
            catch (Exception e)
            {
                Log.Error($"Error {e}");
                throw;
            }
        }


        private static string GetServerName(string solutionFilePath) =>
            ApiServerNames.ServerName<IVsExtensionApi>(solutionFilePath);
    }
}
