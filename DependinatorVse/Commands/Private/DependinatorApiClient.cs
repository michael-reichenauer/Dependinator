using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.Remoting;
using System.Threading.Tasks;
using DependinatorApi;
using DependinatorApi.ApiHandling;


namespace DependinatorVse.Commands.Private
{
    /// <summary>
    ///     Provides functions, which will forward the calls to the Dependinator application.
    /// </summary>
    public class DependinatorApiClient
    {
        private readonly bool isDeveloperStudio;
        private readonly string solutionFilePath;


        public DependinatorApiClient(string solutionFilePath, bool isDeveloperStudio)
        {
            this.solutionFilePath = solutionFilePath;
            this.isDeveloperStudio = isDeveloperStudio;
        }


        public bool IsDependinatorInstalled => File.Exists(GetDependinatorPath());


        /// <summary>
        ///     Show node, which corresponds to the specified file and line number.
        /// </summary>
        public async Task ShowFileAsync(string filePath, int lineNumber)
        {
            await CallAsync<IDependinatorApi>(api => api.ShowNodeForFile(filePath, lineNumber));
        }


        private async Task CallAsync<TRemoteService>(Action<TRemoteService> action)
        {
            string serverName = ApiServerNames.ServerName<TRemoteService>(solutionFilePath);
            Log.Debug($"Calling: {serverName}");

            bool isStartedDependinator = false;

            Stopwatch t = Stopwatch.StartNew();

            while (t.Elapsed < TimeSpan.FromSeconds(20))
            {
                try
                {
                    if (ApiIpcClient.IsServerRegistered(serverName))
                    {
                        using (ApiIpcClient apiIpcClient = new ApiIpcClient(serverName))
                        {
                            action(apiIpcClient.Service<TRemoteService>());
                        }

                        return;
                    }

                    // DependinatorApi not yet registered, lets try to start Dependinator, or wait a little.
                    if (!isStartedDependinator)
                    {
                        isStartedDependinator = true;
                        StartDependinator(solutionFilePath);
                        await Task.Delay(300);
                    }
                    else
                    {
                        await Task.Delay(100);
                    }
                }
                catch (RemotingException e)
                {
                    Log.Warn($"Call error, {e.Message}");
                }
                catch (Exception e)
                {
                    Log.Error($"Failed to check if Dependiator instance is running, {e}");
                    throw;
                }
            }

            Log.Error("Failed to wait for other Dependiator instance");
            throw new Exception("Timeout while waiting for Dependiator to start.");
        }


        private void StartDependinator(string filePath)
        {
            string targetPath = GetDependinatorPath();

            Log.Debug($"Starting: {targetPath}");

            StartProcess(targetPath, Quote(filePath));
        }


        private static bool StartProcess(string targetPath, string arguments)
        {
            try
            {
                Process process = new Process();
                process.StartInfo.FileName = Quote(targetPath);
                process.StartInfo.Arguments = arguments;

                process.StartInfo.WindowStyle = ProcessWindowStyle.Hidden;
                process.StartInfo.CreateNoWindow = true;
                process.StartInfo.UseShellExecute = false;

                process.Start();
                return true;
            }
            catch (Exception e)
            {
                Log.Error($"Failed to start {targetPath} {arguments}, {e}");
                throw;
            }
        }


        private static string Quote(string text)
        {
            char[] QuoteChar = "\"".ToCharArray();
            text = text.Trim();
            text = text.Trim(QuoteChar);
            return $"\"{text}\"";
        }


        private string GetDependinatorPath() => Path.Combine(GetInstallFolderPath(), "Dependinator.exe");


        private string GetInstallFolderPath()
        {
            if (isDeveloperStudio)
            {
                // While developing
                return @"C:\Work Files\Dependinator\Dependinator\bin\Debug";
            }

            string programFolderPath = Environment.ExpandEnvironmentVariables("%ProgramW6432%");

            return Path.Combine(programFolderPath, "Dependinator");
        }
    }
}
