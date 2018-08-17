using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Dependinator.Common.Installation;
using Dependinator.Common.MessageDialogs;
using Dependinator.Common.ModelMetadataFolders;
using Dependinator.Utils;
using DependinatorApi;
using DependinatorApi.ApiHandling;
using Mono.CSharp;


namespace Dependinator.ModelViewing.Private.CodeViewing.Private
{
    internal class SolutionService : ISolutionService
    {
        private readonly IInstaller installer;
        private readonly IMessage message;
        private readonly ModelMetadata metadata;


        public SolutionService(
            IMessage message,
            IInstaller installer,
            ModelMetadata metadata)
        {
            this.message = message;
            this.installer = installer;
            this.metadata = metadata;
        }


        public async Task OpenStudioAsync()
        {
            string solutionFilePath = metadata.ModelFilePath;

            string serverName = ApiServerNames.ServerName<IVsExtensionApi>(solutionFilePath);
            Log.Debug($"Calling: {serverName}");
            bool isStartedDependinator = false;
            Stopwatch t = Stopwatch.StartNew();

            while (t.Elapsed < TimeSpan.FromSeconds(60))
            {
                try
                {
                    if (ApiIpcClient.IsServerRegistered(serverName))
                    {
                        Log.Debug($"Server started after {t.Elapsed}: {serverName}");
                        if (!isStartedDependinator)
                        {
                            using (ApiIpcClient apiIpcClient = new ApiIpcClient(serverName))
                            {
                                apiIpcClient.Service<IVsExtensionApi>().Activate();
                            }
                        }

                        return;
                    }

                    // IVsExtensionApi not yet registered, lets try to start Dependinator, or wait a little.
                    if (!isStartedDependinator)
                    {
                        if (!await TryStartVisualStudioAsync(solutionFilePath)) return;

                        isStartedDependinator = true;
                        t.Restart();
                        await Task.Delay(1000);
                    }
                    else
                    {
                        await Task.Delay(500);
                    }
                }
                catch (Exception e)
                {
                    Log.Error($"Failed to check studio is running {e}");
                }
            }

            Log.Error("Failed to wait for other Dependiator instance");
        }


        public async Task OpenFileAsync(string filePath, int lineNumber)
        {
            string solutionFilePath = metadata.ModelFilePath;

            string serverName = ApiServerNames.ServerName<IVsExtensionApi>(solutionFilePath);

            if (!ApiIpcClient.IsServerRegistered(serverName))
            {
                await OpenStudioAsync();
            }

            if (ApiIpcClient.IsServerRegistered(serverName))
            {
                using (ApiIpcClient apiIpcClient = new ApiIpcClient(serverName))
                {
                    apiIpcClient.Service<IVsExtensionApi>().ShowFile(filePath, lineNumber);
                    apiIpcClient.Service<IVsExtensionApi>().Activate();
                }
            }
        }


        private async Task<bool> TryStartVisualStudioAsync(string solutionFilePath)
        {
            if (!await IsExtensionInstalledAsync())
            {
                if (!message.ShowAskOkCancel(
                    "The Visual Studio Dependinator extension does not seem to be installed.\n\n" +
                    "Please install the latest release.\n" +
                    "You may need to restart running Visual Studio instances."))
                {
                    return false;
                }

                if (!installer.InstallExtension(false, true) || !installer.IsExtensionInstalled())
                {
                    message.ShowWarning(
                        "The Visual Studio Dependinator extension does not\n" +
                        "seem to have been installed.");
                    return false;
                }
            }

            StartVisualStudio(solutionFilePath);
            return true;
        }


        private Task<bool> IsExtensionInstalledAsync() => Task.Run(() => installer.IsExtensionInstalled());

        private static void StartVisualStudio(string solutionPath)
        {
            if (BuildConfig.IsDebug)
            {
                StartVisualStudioDebug(solutionPath);
                return;
            }

            try
            {
                Process process = new Process();
                process.StartInfo.FileName = solutionPath;
                process.StartInfo.UseShellExecute = true;

                process.Start();
            }
            catch (Exception ex) when (ex.IsNotFatal())
            {
                Log.Error($"Failed to start Visual Studio {ex}");
            }
        }


        private static void StartVisualStudioDebug(string solutionPath)
        {
            try
            {
                if (TryGetStdioExePath(out string exePath))
                {
                    Process process = new Process();
                    process.StartInfo.FileName = $"\"{exePath}\"";
                    process.StartInfo.Arguments = $"/rootsuffix Exp \"{solutionPath}\"";

                    process.StartInfo.WindowStyle = ProcessWindowStyle.Hidden;
                    process.StartInfo.CreateNoWindow = true;
                    process.StartInfo.UseShellExecute = false;

                    process.Start();
                }
            }
            catch (Exception e)
            {
                Log.Error($"Failed to start Visual Studio, {e}");
            }
        }


        private static bool TryGetStdioExePath(out string studioExePath)
        {
            string studioPath = StudioPath();

            studioExePath = Directory
                .GetFiles(studioPath, "devenv.exe", SearchOption.AllDirectories)
                .FirstOrDefault();
            Log.Debug($"Studio exe path {studioPath}");

            return studioExePath != null;
        }


        private static string StudioPath()
        {
            string programFiles = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86);
            return Path.Combine(programFiles, "Microsoft Visual Studio");
        }
    }
}
