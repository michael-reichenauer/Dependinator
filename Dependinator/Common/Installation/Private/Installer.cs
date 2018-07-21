using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using Dependinator.Common.Environment;
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


		public bool IsExtensionInstalled()
		{
			string studioPath = StudioPath();

			string filePath = Directory
				.GetFiles(studioPath, "DependinatorVse.dll", SearchOption.AllDirectories)
				.FirstOrDefault();

			return filePath != null;
		}


		private void InstallSilent()
		{
			Log.Usage("Installing ...");
			CreateMainExeShortcut();
			CleanOldInstallations();

			if (commandLine.IsInstallExtension || IsExtensionInstalled())
			{
				InstallExtension(true, false);
			}

			Log.Usage("Installed");
		}


		public bool InstallExtension(bool isSilent, bool isWait)
		{
			if (!TryGetExtensionPath(out string extensionPath)) return false;

			string args = $"/a /f \"{extensionPath}\"";
			if (isSilent)
			{
				args = "/q " + args;
			}

			return RunVxixInstaller(args, isWait);
		}


		private void UninstallSilent()
		{
			Log.Debug("Uninstalling...");
			UninstallExtension(false);
			Log.Debug("Uninstalled");
		}


		private void CreateMainExeShortcut()
		{
			Log.Debug("Create main exe shortcut");
			string sourcePath = ProgramInfo.Location;
			Version sourceVersion = Version.Parse(ProgramInfo.Version);

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


		private static void UninstallExtension(bool isWait)
		{
			RunVxixInstaller("/a /q /u:\"0117fbb8-b3c3-4baf-858e-d7ed140c01f4\"", isWait);
		}


		private static bool RunVxixInstaller(string arguments, bool isWait)
		{
			if (!TryGetVsixInstallerPath(out string vsixInstallerPath))
			{
				Log.Warn("No VsixInstaller found");
				return false; 
			}

			try
			{
				Process process = new Process();
				process.StartInfo.FileName = $"\"{vsixInstallerPath}\"";
				process.StartInfo.Arguments = arguments;
				process.StartInfo.WindowStyle = ProcessWindowStyle.Hidden;
				process.StartInfo.CreateNoWindow = true;
				process.StartInfo.UseShellExecute = false;

				process.Start();
				if (isWait)
				{
					process.WaitForExit();
				}

				return true;
			}
			catch (Exception e)
			{
				Log.Error($"Failed to start VSIXInstaller, {e}");
				return false;
			}
		}


		private static bool TryGetExtensionPath(out string extensionPath)
		{
			string installedFilesFolderPath = ProgramInfo.GetInstalledFilesFolderPath();
			extensionPath = Path.Combine(installedFilesFolderPath, "DependinatorVse.vsix");
			Log.Debug($"DependinatorVse path: {extensionPath}");

			return File.Exists(extensionPath);
		}


		private static bool TryGetVsixInstallerPath(out string vsixInstallerPath)
		{
			string studioPath = StudioPath();

			vsixInstallerPath = Directory
				.GetFiles(studioPath, "VSIXInstaller.exe", SearchOption.AllDirectories)
				.FirstOrDefault();
			Log.Debug($"VSIXInstaller path {vsixInstallerPath}");

			return vsixInstallerPath != null;
		}


		private static string StudioPath()
		{
			string programFiles = System.Environment.GetFolderPath(System.Environment.SpecialFolder.ProgramFilesX86);
			return Path.Combine(programFiles, "Microsoft Visual Studio");
		}
	}
}