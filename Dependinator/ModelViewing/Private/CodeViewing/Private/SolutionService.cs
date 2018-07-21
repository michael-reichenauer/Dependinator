using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Dependinator.Common.MessageDialogs;
using Dependinator.Common.ModelMetadataFolders;
using Dependinator.Utils;
using DependinatorApi;
using DependinatorApi.ApiHandling;


namespace Dependinator.ModelViewing.Private.CodeViewing.Private
{
	internal class SolutionService : ISolutionService
	{
		private readonly IMessage message;
		private readonly ModelMetadata metadata;


		public SolutionService(
			IMessage message,
			ModelMetadata metadata)
		{
			this.message = message;
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
						if (!IsExtensionInstalled())
						{
							if (!message.ShowAskOkCancel(
								"The Visual Studio Dependinator extension does not seem to be installed.\n\n" +
								"Please install the latest release.\n" +
								"You may need to restart running Visual Studio instances."))
							{
								return;
							}

							if (!TryInstallExtension() || !IsExtensionInstalled())
							{
								message.ShowWarning("The Visual Studio Dependinator extension does not\n"+
								                    "seem to have been installed." );
								return;
							}
						}

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


		private static bool TryInstallExtension()
		{
			string studioPath = StudioPath();

			string filePath = Directory
				.GetFiles(studioPath, "VSIXInstaller.exe", SearchOption.AllDirectories)
				.FirstOrDefault();

			if (filePath == null)
			{
				return false;
			}

			string installedFilesFolderPath = ProgramInfo.GetInstalledFilesFolderPath();
			string extensionPath = Path.Combine(installedFilesFolderPath, "DependinatorVse.vsix");
			if (!File.Exists(extensionPath))
			{
				return false;
			}
		
			try
			{
				Process process = new Process();
				process.StartInfo.FileName = Quote(filePath);
				process.StartInfo.Arguments = $"/a \"{extensionPath}\"";

				process.StartInfo.WindowStyle = ProcessWindowStyle.Hidden;
				process.StartInfo.CreateNoWindow = true;
				process.StartInfo.UseShellExecute = false;

				process.Start();
				process.WaitForExit((int)TimeSpan.FromMinutes(5).TotalMilliseconds);
			}
			catch (Exception e)
			{
				Log.Error($"Failed to start studio, {e}");
			}

			return true;
		}


		private static bool IsExtensionInstalled()
		{
			string studioPath = StudioPath();

			string filePath = Directory
				.GetFiles(studioPath, "DependinatorVse.dll", SearchOption.AllDirectories)
				.FirstOrDefault();

			return !string.IsNullOrEmpty(filePath);
		}


		private static string StudioPath()
		{
			string programFiles = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86);
			string studioPath = Path.Combine(programFiles, "Microsoft Visual Studio");
			return studioPath;
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


		private static void StartVisualStudioDebug(string solutionPath)
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
				Log.Error($"Failed to start Visual Studio, {e}");
			}
		}


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
				process.Start();
			}
			catch (Exception ex) when (ex.IsNotFatal())
			{
				Log.Error($"Failed to start Visual Studio {ex}");
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