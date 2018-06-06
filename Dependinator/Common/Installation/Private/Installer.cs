using System;
using System.IO;
using System.Linq;
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
			CreateMainExeShortcut();
			CleanOldInstallations();

			Log.Usage("Installed");
		}


		private void UninstallSilent()
		{
			Log.Debug("Uninstalling...");
			Log.Debug("Uninstalled");
		}


		private void CreateMainExeShortcut()
		{
			Log.Debug("Create main exe shortcut");
			string sourcePath = Program.Location;
			Version sourceVersion = Version.Parse(Program.Version);

			string targetFolder = ProgramInfo.GetDataFolderPath();

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
		}


		private void WriteInstalledVersion(Version sourceVersion)
		{
			try
			{
				string path = ProgramInfo.GetInstallVersionFilePath();
				File.WriteAllText(path, sourceVersion.ToString());
				Log.Debug($"Installed {sourceVersion}");
			}
			catch (Exception e)
			{
				Log.Exception(e, "Failed to write version");
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


		private void CleanOldInstallations()
		{
			Log.Debug("Try to clean old versions if needed");
			string basePath = ProgramInfo.GetInstallFolderPath();

			// Get old installed versions, but leave the 3 newest)
			var oldVersions = Directory
				.GetDirectories(basePath)
				.Select(path => Path.GetFileName(path))
				.Where(name => Version.TryParse(name, out _))
				.Select(name => Version.Parse(name))
				.OrderByDescending(version => version)
				.Skip(3);

			foreach (Version version in oldVersions)
			{
				string path = Path.Combine(basePath, version.ToString());
				DeleteOldVersion(path);
			}
		}


		private void DeleteOldVersion(string path)
		{
			try
			{
				Log.Debug($"Try to delete {path}");
				Directory.Delete(path, true);
			}
			catch (Exception e)
			{
				Log.Exception(e, $"Failed to delete {path}");
			}
		}
	}
}