using System;
using System.IO;
using Dependinator.Common.Environment;
using Dependinator.Common.SettingsHandling;
using Dependinator.Utils;


namespace Dependinator.Common.Installation.Private
{
	internal class Installer : IInstaller
	{
		private readonly ICommandLine commandLine;


		public Installer(ICommandLine commandLine)
		{
			this.commandLine = commandLine;
		}


		public bool InstallOrUninstall()
		{
			if (commandLine.IsInstall && commandLine.IsSilent)
			{
				InstallSilent();

				return false;
			}
			else if (commandLine.IsUninstall && commandLine.IsSilent)
			{
				UninstallSilent();

				return false;
			}

			return true;
		}


		private void InstallSilent()
		{
			Log.Usage("Installing ...");
			string path = CopyFileToProgramFiles();

			Log.Usage("Installed");
		}


		private void UninstallSilent()
		{
			Log.Debug("Uninstalling...");
			Log.Debug("Uninstalled");
		}


		private string CopyFileToProgramFiles()
		{
			string sourcePath = ProgramInfo.GetCurrentInstancePath();
			Version sourceVersion = ProgramInfo.GetVersion(sourcePath);

			string targetFolder = ProgramInfo.GetProgramFolderPath();

			EnsureDirectoryIsCreated(targetFolder);

			string targetPath = ProgramInfo.GetInstallFilePath();

			try
			{
				if (sourcePath != targetPath)
				{
					CopyFile(sourcePath, targetPath);
					WriteInstalledVersion(sourceVersion);
				}
			}
			catch (Exception e) when (e.IsNotFatal())
			{
				Log.Debug($"Failed to copy {sourcePath} to target {targetPath} {e.Message}");
				try
				{
					string oldFilePath = ProgramInfo.GetTempFilePath();
					Log.Debug($"Moving {targetPath} to {oldFilePath}");

					File.Move(targetPath, oldFilePath);
					Log.Debug($"Moved {targetPath} to target {oldFilePath}");
					CopyFile(sourcePath, targetPath);
					WriteInstalledVersion(sourceVersion);
				}
				catch (Exception ex) when (e.IsNotFatal())
				{
					Log.Error($"Failed to copy {sourcePath} to target {targetPath}, {ex}");
					throw;
				}
			}

			return targetPath;
		}


		private void WriteInstalledVersion(Version sourceVersion)
		{
			try
			{
				string path = ProgramInfo.GetVersionFilePath();
				File.WriteAllText(path, sourceVersion.ToString());
				Log.Debug($"Installed {sourceVersion}");
			}
			catch (Exception e)
			{
				Log.Warn($"Failed to write version {e}");
			}
		}


		private static void CopyFile(string sourcePath, string targetPath)
		{
			// Not using File.Copy, to avoid copying possible "downloaded from internet flag"
			byte[] fileData = File.ReadAllBytes(sourcePath);
			File.WriteAllBytes(targetPath, fileData);
			Log.Debug($"Copied {sourcePath} to target {targetPath}");
		}


		private static void EnsureDirectoryIsCreated(string targetFolder)
		{
			if (!Directory.Exists(targetFolder))
			{
				Directory.CreateDirectory(targetFolder);
			}
		}
	}
}