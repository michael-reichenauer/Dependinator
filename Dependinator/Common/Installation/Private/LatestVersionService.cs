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
using Dependinator.Utils.Net;
using Microsoft.Win32;


namespace Dependinator.Common.Installation.Private
{
	[SingleInstance]
	internal class LatestVersionService : ILatestVersionService
	{
		private static readonly TimeSpan IdleTimeBeforeRestarting = TimeSpan.FromMinutes(30);
		private static readonly TimeSpan FirstCheckTime = TimeSpan.FromSeconds(1);
		private static readonly TimeSpan CheckInterval = TimeSpan.FromHours(3);

		private static readonly string latestUri =
			"https://api.github.com/repos/michael-reichenauer/Dependinator/releases/latest";
		private static readonly string UserAgent = "Dependinator";

		private readonly ISettingsService settingsService;
		private readonly ICmd cmd;
		private readonly IStartInstanceService startInstanceService;
		private readonly ModelMetadata modelMetadata;

		private DispatcherTimer checkTimer;
		private DispatcherTimer idleTimer;


		public LatestVersionService(
			ICmd cmd,
			IStartInstanceService startInstanceService,
			ISettingsService settingsService,
			ModelMetadata modelMetadata)
		{
			this.cmd = cmd;
			this.startInstanceService = startInstanceService;
			this.settingsService = settingsService;
			this.modelMetadata = modelMetadata;
		}


		public event EventHandler OnNewVersionAvailable;

		public void StartCheckForLatestVersion()
		{
			checkTimer = new DispatcherTimer();
			checkTimer.Tick += CheckLatestVersionAsync;
			checkTimer.Interval = FirstCheckTime;
			checkTimer.Start();

			idleTimer = new DispatcherTimer();
			idleTimer.Tick += CheckIdleBeforeRestart;
			idleTimer.Interval = TimeSpan.FromMinutes(1);

			SystemEvents.PowerModeChanged += OnPowerModeChange;
		}


		private async void CheckLatestVersionAsync(object sender, EventArgs e)
		{
			checkTimer.Interval = CheckInterval;

			if (settingsService.Get<Options>().DisableAutoUpdate)
			{
				Log.Info("DisableAutoUpdate = true");
				return;
			}

			if (await IsNewRemoteVersionAvailableAsync())
			{
				await InstallLatestVersionAsync();

				// The actual installation (copy of files) is done by another, allow some time for that
				await Task.Delay(TimeSpan.FromSeconds(5));
			}

			NotifyIfNewVersionIsAvailable();
		}


		private async Task<bool> IsNewRemoteVersionAvailableAsync()
		{
			Log.Debug($"Checking remote version of {latestUri} ...");
			Version remoteVersion = await GetLatestRemoteVersionAsync();
			Version currentVersion = ProgramInfo.GetCurrentInstanceVersion();
			Version installedVersion = ProgramInfo.GetInstalledVersion();

			LogVersion(currentVersion, installedVersion, remoteVersion);
			return installedVersion < remoteVersion;
		}


		private async Task<bool> InstallLatestVersionAsync()
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
					string setupPath = await DownloadSetupAsync(httpClient, latestInfo);

					InstallDownloadedSetup(setupPath);
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
			Asset setupFileInfo = latestInfo.assets.First(a => a.name == $"{Product.Name}Setup.exe");

			string downloadUrl = setupFileInfo.browser_download_url;
			Log.Info($"Downloading {latestInfo.tag_name} from {downloadUrl} ...");

			Timing t = Timing.Start();
			httpClient.ProgressChanged += (totalFileSize, totalBytesDownloaded, progressPercentage) =>
			{
				Log.Info($"Downloading {latestInfo.tag_name} {progressPercentage}% (time: {t.Elapsed}) ...");
			};

			string setupPath = ProgramInfo.GetTempFilePath() + "." + setupFileInfo.name;
			await httpClient.StartDownloadAsync(downloadUrl, setupPath);

			Log.Info($"Downloaded {latestInfo.tag_name} to {setupPath}");
			return setupPath;
		}


		private void InstallDownloadedSetup(string setupPath)
		{
			cmd.Start(setupPath, "/install /silent");
		}


		private async Task<Version> GetLatestRemoteVersionAsync()
		{
			try
			{
				R<LatestInfo> latestInfo = await GetLatestInfoAsync();

				if (latestInfo.IsOk && latestInfo.Value.tag_name != null)
				{
					Version version = Version.Parse(latestInfo.Value.tag_name.Substring(1));
					Log.Debug($"Remote version: {version}");

					if (latestInfo.Value.assets != null)
					{
						foreach (var asset in latestInfo.Value.assets)
						{
							Log.Debug($"Name: {asset.name}, Count: {asset.download_count}");
						}
					}

					return version;
				}
			}
			catch (Exception e) when (e.IsNotFatal())
			{
				Log.Warn($"Failed to get latest version {e}");
			}

			return new Version(0, 0, 0, 0);
		}


		private async Task<R<LatestInfo>> GetLatestInfoAsync()
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
						Log.Debug("Remote latest version info same as cached info");
						return GetCachedLatestVersionInfo();
					}
					else
					{
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
			}
			catch (Exception e) when (e.IsNotFatal())
			{
				Log.Warn($"Failed to download latest setup: {e}");
				return e;
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


		private static void LogVersion(Version current, Version installed, Version remote)
		{
			Log.Usage($"Version current: {current}, installed: {installed} remote: {remote}");
		}


		private void NotifyIfNewVersionIsAvailable()
		{
			if (IsNewVersionInstalled())
			{
				OnNewVersionAvailable?.Invoke(this, EventArgs.Empty);

				if (!idleTimer.IsEnabled)
				{
					Log.Debug($"Waiting for idle {IdleTimeBeforeRestarting} before restarting newer version ...");
					idleTimer.Start();
				}
			}
			else
			{
				if (idleTimer.IsEnabled)
				{
					Log.Debug("No longer newer version installed, canceling idle wait for restart");
					idleTimer.Stop();
				}
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


		void CheckIdleBeforeRestart(object sender, EventArgs e)
		{
			TimeSpan idleTime = SystemIdle.GetLastInputIdleTimeSpan();
			if (idleTime > IdleTimeBeforeRestarting)
			{
				// Track.Info($"Idle time {idleTime}, trigger restart if newer is installed");
				idleTimer.Stop();

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
			Version currentVersion = ProgramInfo.GetCurrentInstanceVersion();
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
			public string tag_name;
			public Asset[] assets;
		}


		// Type used when parsing latest version information json
		internal class Asset
		{
			public string name;
			public int download_count;
			public string browser_download_url;
		}
	}
}