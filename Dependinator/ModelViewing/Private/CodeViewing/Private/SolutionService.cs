using System;
using System.Diagnostics;
using System.Threading.Tasks;
using Dependinator.Common.ModelMetadataFolders;
using Dependinator.Utils;
using DependinatorApi;
using DependinatorApi.ApiHandling;


namespace Dependinator.ModelViewing.Private.CodeViewing
{
	internal class SolutionService : ISolutionService
	{
		private readonly ModelMetadata metadata;


		public SolutionService(ModelMetadata metadata)
		{
			this.metadata = metadata;
		}


		public async Task OpenAsync()
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
						isStartedDependinator = true;
						StartVisualStudio(solutionFilePath);
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
				await OpenAsync();
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


		private static void StartVisualStudio(string solutionPath)
		{
			try
			{
				Process process = new Process();
				process.StartInfo.FileName =
					Quote(@"C:\Program Files (x86)\Microsoft Visual Studio\2017\Community\Common7\IDE\devenv.exe");
				process.StartInfo.Arguments = $"/rootsuffix Exp \"{solutionPath}\"";

				process.StartInfo.WindowStyle = ProcessWindowStyle.Hidden;
				process.StartInfo.CreateNoWindow = true;
				process.StartInfo.UseShellExecute = false;

				process.Start();
			}
			catch (Exception e)
			{
				Log.Error($"Failed to start studio, {e}");
			}
		}


		private static void StartVisualStudioOrg(string solutionPath)
		{

			try
			{
				Process process = new Process();
				process.StartInfo.FileName = solutionPath;
				process.Start();
			}
			catch (Exception ex) when (ex.IsNotFatal())
			{
				Log.Error($"Failed to open help link {ex}");
			}

		}


		private static string Quote(string text)
		{
			char[] QuoteChar = "\"".ToCharArray();

			text = text.Trim();
			text = text.Trim(QuoteChar);
			return $"\"{text}\"";
		}

	}
}