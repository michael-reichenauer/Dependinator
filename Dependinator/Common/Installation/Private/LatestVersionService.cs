using System;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;
using Dependinator.Common.ModelMetadataFolders;
using Dependinator.Common.ModelMetadataFolders.Private;
using Dependinator.Common.SettingsHandling;
using Dependinator.Utils;
using Dependinator.Utils.Dependencies;
using Dependinator.Utils.ErrorHandling;
using Dependinator.Utils.Net;
using Dependinator.Utils.OsSystem;
using Dependinator.Utils.Serialization;
using Dependinator.Utils.Threading;
using Microsoft.Win32;


namespace Dependinator.Common.Installation.Private
{
    [SingleInstance]
    internal class LatestVersionService : ILatestVersionService
    {
        private static readonly TimeSpan IdleTimerInterval = TimeSpan.FromMinutes(1);
        private static readonly TimeSpan IdleTimeBeforeRestarting = TimeSpan.FromMinutes(10);

        private static readonly string latestUri =
            "https://api.github.com/repos/michael-reichenauer/Dependinator/releases/latest";

        private static readonly string UserAgent = "Dependinator";
        private readonly ModelMetadata modelMetadata;

        private readonly ISettingsService settingsService;

        private readonly IStartInstanceService startInstanceService;

        private DispatcherTimer idleTimer;
        private DateTime lastIdleCheck = DateTime.MaxValue;


        public LatestVersionService(
            IStartInstanceService startInstanceService,
            ISettingsService settingsService,
            ModelMetadata modelMetadata)
        {
            this.startInstanceService = startInstanceService;
            this.settingsService = settingsService;
            this.modelMetadata = modelMetadata;
        }


        public event EventHandler OnNewVersionAvailable;


        public void StartCheckForLatestVersion()
        {
            idleTimer = new DispatcherTimer();
            idleTimer.Tick += CheckIdleBeforeRestart;
            idleTimer.Interval = IdleTimerInterval;
            idleTimer.Start();

            SystemEvents.PowerModeChanged += OnPowerModeChange;
        }


        public async Task CheckLatestVersionAsync()
        {
            if (settingsService.Get<Options>().DisableAutoUpdate)
            {
                return;
            }

            if (await IsNewRemoteVersionAvailableAsync())
            {
                await DownloadLatestVersionAsync();
                await IsNewRemoteVersionAvailableAsync();
            }
        }


        private async Task<bool> IsNewRemoteVersionAvailableAsync()
        {
            Version remoteVersion = await GetLatestRemoteVersionAsync();
            Version currentVersion = Version.Parse(ProgramInfo.Version);
            Version installedVersion = ProgramInfo.GetInstalledVersion();

            Version setupVersion = ProgramInfo.GetSetupVersion();

            LogVersion(currentVersion, installedVersion, remoteVersion, setupVersion);

            return installedVersion < remoteVersion && setupVersion < remoteVersion;
        }


        private async Task<bool> DownloadLatestVersionAsync()
        {
            try
            {
                Log.Info($"Downloading remote setup {latestUri} ...");

                LatestInfo latestInfo = GetCachedLatestVersionInfo();
                if (latestInfo == null)
                {
                    // No installed version.
                    return false;
                }

                using (HttpClientDownloadWithProgress httpClient = GetDownloadHttpClient())
                {
                    await DownloadSetupAsync(httpClient, latestInfo);

                    return true;
                }
            }
            catch (Exception e) when (e.IsNotFatal())
            {
                Log.Error($"Failed to install new version {e}");
            }

            return false;
        }


        private static async Task<string> DownloadSetupAsync(
            HttpClientDownloadWithProgress httpClient, LatestInfo latestInfo)
        {
            Asset setupFileInfo = latestInfo.assets.First(a => a.name == $"{ProgramInfo.Name}Setup.exe");

            string downloadUrl = setupFileInfo.browser_download_url;
            Log.Info($"Downloading {latestInfo.tag_name} from {downloadUrl} ...");

            Timing t = Timing.Start();
            httpClient.ProgressChanged += (totalFileSize, totalBytesDownloaded, progressPercentage) =>
            {
                Log.Info($"Downloading {latestInfo.tag_name} {progressPercentage}% (time: {t.Elapsed}) ...");
            };

            string setupPath = ProgramInfo.GetSetupFilePath();

            if (File.Exists(setupPath))
            {
                File.Delete(setupPath);
            }

            await httpClient.StartDownloadAsync(downloadUrl, setupPath);

            Log.Info($"Downloaded {latestInfo.tag_name} to {setupPath}");
            return setupPath;
        }


        private async Task<Version> GetLatestRemoteVersionAsync()
        {
            try
            {
                M<LatestInfo> latestInfo = await GetLatestInfoAsync();

                if (latestInfo.IsOk && latestInfo.Value.tag_name != null)
                {
                    Version version = Version.Parse(latestInfo.Value.tag_name.Substring(1));

                    return version;
                }
            }
            catch (Exception e) when (e.IsNotFatal())
            {
                Log.Warn($"Failed to get latest version {e}");
            }

            return new Version(0, 0, 0, 0);
        }


        private async Task<M<LatestInfo>> GetLatestInfoAsync()
        {
            try
            {
                using (HttpClient httpClient = new HttpClient())
                {
                    httpClient.DefaultRequestHeaders.Add("user-agent", UserAgent);

                    // Try get cached information about latest remote version
                    string eTag = GetCachedLatestVersionInfoEtag();

                    if (!string.IsNullOrEmpty(eTag))
                    {
                        // There is cached information, lets use the ETag when checking to follow
                        // GitHub Rate Limiting method.
                        httpClient.DefaultRequestHeaders.IfNoneMatch.Clear();
                        httpClient.DefaultRequestHeaders.IfNoneMatch.Add(new EntityTagHeaderValue(eTag));
                    }

                    HttpResponseMessage response = await httpClient.GetAsync(latestUri);

                    if (response.StatusCode == HttpStatusCode.NotModified || response.Content == null)
                    {
                        return GetCachedLatestVersionInfo();
                    }

                    string latestInfoText = await response.Content.ReadAsStringAsync();
                    Log.Debug("New version info");

                    if (response.Headers.ETag != null)
                    {
                        eTag = response.Headers.ETag.Tag;
                        CacheLatestVersionInfo(eTag, latestInfoText);
                    }

                    return Json.As<LatestInfo>(latestInfoText);
                }
            }
            catch (Exception e) when (e.IsNotFatal())
            {
                Log.Exception(e, "Failed to download latest setup");
                return Error.From(e);
            }
        }


        private LatestInfo GetCachedLatestVersionInfo()
        {
            ProgramSettings programSettings = settingsService.Get<ProgramSettings>();

            return Json.As<LatestInfo>(programSettings.LatestVersionInfo);
        }


        private string GetCachedLatestVersionInfoEtag()
        {
            ProgramSettings programSettings = settingsService.Get<ProgramSettings>();
            return programSettings.LatestVersionInfoETag;
        }


        private void CacheLatestVersionInfo(string eTag, string latestInfoText)
        {
            if (string.IsNullOrEmpty(eTag)) return;

            // Cache the latest version info
            settingsService.Edit<ProgramSettings>(s =>
            {
                s.LatestVersionInfoETag = eTag;
                s.LatestVersionInfo = latestInfoText;
            });
        }


        private static void LogVersion(Version current, Version installed, Version remote, Version setup)
        {
            Log.Usage(
                $"Version current: {current}, installed: {installed}, remote: {remote}, setup {setup}");
        }


        private void NotifyIfNewVersionIsAvailable()
        {
            if (IsNewVersionInstalled())
            {
                OnNewVersionAvailable?.Invoke(this, EventArgs.Empty);
            }
        }


        private void OnPowerModeChange(object sender, PowerModeChangedEventArgs e)
        {
            Log.Info($"Power mode {e.Mode}");

            if (e.Mode == PowerModes.Resume)
            {
                if (IsNewVersionInstalled())
                {
                    Log.Info("Newer version is installed, restart ...");
                    if (startInstanceService.StartInstance(modelMetadata.ModelFilePath))
                    {
                        // Newer version is started, close this instance
                        Application.Current.Shutdown(0);
                    }
                }
            }
        }


        private void CheckIdleBeforeRestart(object sender, EventArgs e)
        {
            TimeSpan timeSinceCheck = DateTime.UtcNow - lastIdleCheck;
            bool wasSleeping = false;

            if (timeSinceCheck > IdleTimerInterval + TimeSpan.FromMinutes(1))
            {
                // The timer did not tick within the expected timeout, thus computer was probably sleeping. 
                Log.Info($"Idle timer timeout, was: {timeSinceCheck}");
                wasSleeping = true;
            }

            lastIdleCheck = DateTime.UtcNow;

            TimeSpan idleTime = SystemIdle.GetLastInputIdleTimeSpan();
            if (wasSleeping || idleTime > IdleTimeBeforeRestarting)
            {
                if (IsNewVersionInstalled())
                {
                    if (startInstanceService.StartInstance(modelMetadata.ModelFilePath))
                    {
                        // Newer version is started, close this instance
                        Application.Current.Shutdown(0);
                    }
                }
            }
        }


        private static bool IsNewVersionInstalled()
        {
            Version currentVersion = Version.Parse(ProgramInfo.Version);
            Version installedVersion = ProgramInfo.GetInstalledVersion();

            Log.Debug($"Current version: {currentVersion} installed version: {installedVersion}");
            return currentVersion < installedVersion;
        }


        private static HttpClientDownloadWithProgress GetDownloadHttpClient()
        {
            HttpClientDownloadWithProgress client = new HttpClientDownloadWithProgress();
            client.NetworkActivityTimeout = TimeSpan.FromSeconds(30);
            client.HttpClient.Timeout = TimeSpan.FromSeconds(60 * 5);

            client.HttpClient.DefaultRequestHeaders.Add("user-agent", UserAgent);

            return client;
        }


        // Type used when parsing latest version information json
        public class LatestInfo
        {
            public Asset[] assets;
            public string tag_name;
        }


        // Type used when parsing latest version information json
        internal class Asset
        {
            public string browser_download_url;
            public int download_count;
            public string name;
        }
    }
}
