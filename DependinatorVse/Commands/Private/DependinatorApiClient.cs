using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using DependinatorApi;
using DependinatorApi.ApiHandling;


namespace DependinatorVse.Commands.Private
{
	public class DependinatorApiClient
	{
		private static readonly string Name = "Dependinator";
		private static readonly char[] QuoteChar = "\"".ToCharArray();

		private readonly string solutionFilePath;


		public DependinatorApiClient(string solutionFilePath)
		{
			this.solutionFilePath = solutionFilePath;
		}


		public async Task ShowFileAsync(string filePath)
		{
			await CallAsync<IDependinatorApi>(api => api.ShowFile(filePath));
		}


		private async Task CallAsync<TRemoteService>(Action<TRemoteService> action)
		{
			string serverName = DependinatorApiHelper.ServerName(solutionFilePath);
			Log.Debug($"Calling: {serverName}");

			bool isStartedDependinator = false;
			
			Stopwatch t = Stopwatch.StartNew();

			while (t.Elapsed < TimeSpan.FromSeconds(5))
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
						StartedDependinator(solutionFilePath);
						await Task.Delay(300);
					}
					else
					{
						await Task.Delay(100);
					}
				}
				catch (Exception e)
				{
					Log.Error($"Failed to check if Dependiator instance is running {e}");
				}
			}

			Log.Error("Failed to wait for other Dependiator instance");
		}


		private static void StartedDependinator(string filePath)
		{
			string targetPath = Path.Combine(GetInstallFolderPath(), $"{Name}.exe");

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
				return false;
			}
		}


		private static string Quote(string text)
		{
			text = text.Trim();
			text = text.Trim(QuoteChar);
			return $"\"{text}\"";
		}


		public static string GetInstallFolderPath()
		{
			string programFolderPath = Environment.ExpandEnvironmentVariables("%ProgramW6432%");

			return Path.Combine(programFolderPath, Name);
		}
	}
}