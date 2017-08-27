using System;
using System.IO;
using System.Threading;
using Dependinator.Common.MessageDialogs;
using Dependinator.Common.SettingsHandling;
using Dependinator.Utils;
using Microsoft.Win32;

namespace Dependinator.Common.Installation.Private
{
	internal class Installer : IInstaller
	{
		private readonly ICommandLine commandLine;
		private static readonly string ProductNameLowercase = Product.Name.ToLowerInvariant();


		private static readonly string UninstallSubKey =
			$"SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\Uninstall\\{{{Product.Guid}}}_is1";
		private static readonly string UninstallRegKey = "HKEY_CURRENT_USER\\" + UninstallSubKey;
		private static readonly string subFolderContextMenuPath =
			$"Software\\Classes\\Folder\\shell\\{ProductNameLowercase}";
		private static readonly string subdllContextMenuPath =
			$"Software\\Classes\\*\\shell\\{ProductNameLowercase}";
		private static readonly string subDirectoryBackgroundContextMenuPath =
			$"Software\\Classes\\Directory\\Background\\shell\\{ProductNameLowercase}";
		private static readonly string folderContextMenuPath =
			"HKEY_CURRENT_USER\\" + subFolderContextMenuPath;
		private static readonly string dllContextMenuPath =
			"HKEY_CURRENT_USER\\" + subdllContextMenuPath;
		private static readonly string directoryContextMenuPath =
			"HKEY_CURRENT_USER\\" + subDirectoryBackgroundContextMenuPath;
		private static readonly string folderCommandContextMenuPath =
			folderContextMenuPath + "\\command";
		private static readonly string dllCommandContextMenuPath =
			dllContextMenuPath + "\\command";
		private static readonly string directoryCommandContextMenuPath =
			directoryContextMenuPath + "\\command";
		private static readonly string SetupTitle = $"{Product.Name} - Setup";


		private readonly ICmd cmd;


		public Installer(ICommandLine commandLine, ICmd cmd)
		{
			this.commandLine = commandLine;
			this.cmd = cmd;
		}


		public bool InstallOrUninstall()
		{
			if (commandLine.IsInstall && !commandLine.IsSilent)
			{
				InstallNormal();

				return false;
			}
			else if (commandLine.IsInstall && commandLine.IsSilent)
			{
				InstallSilent();

				if (commandLine.IsRunInstalled)
				{
					StartInstalled();
				}

				return false;
			}
			else if (commandLine.IsUninstall && !commandLine.IsSilent)
			{
				UninstallNormal();

				return false;
			}
			else if (commandLine.IsUninstall && commandLine.IsSilent)
			{
				UninstallSilent();

				return false;
			}

			return true;
		}


		private void InstallNormal()
		{
			Log.Usage("Install normal.");

			if (!Message.ShowAskOkCancel(
				$"Welcome to the {Product.Name} setup.\n\n" +
				" This will:\n" +
				$" - Add a {Product.Name} shortcut in the Start Menu.\n" +
				$" - Add a {Product.Name} context menu item in Windows File Explorer.\n" +
				$" - Make {Product.Name} command available in Command Prompt window.\n\n" +
				$"Click OK to install {Product.Name} or Cancel to exit Setup.",
				SetupTitle))
			{
				return;
			}

			if (!EnsureNoOtherInstancesAreRunning())
			{
				return;
			}

			InstallSilent();
			Log.Usage("Installed normal.");

			Message.ShowInfo(
				$"Setup has finished installing {Product.Name}.",
				SetupTitle);

			StartInstalled();
		}



		private void StartInstalled()
		{
			string targetPath = ProgramInfo.GetInstallFilePath();
			cmd.Start(targetPath, "");
		}


		private static bool EnsureNoOtherInstancesAreRunning()
		{
			while (true)
			{
				bool created = false;
				using (new Mutex(true, Product.Guid, out created))
				{
					if (created)
					{
						return true;
					}

					Log.Debug($"{Product.Name} instance is already running, needs to be closed.");
					if (!Message.ShowAskOkCancel(
						$"Please close all instances of {Product.Name} before continue the installation."))
					{
						return false;
					}
				}
			}
		}


		private void InstallSilent()
		{
			Log.Usage("Installing ...");
			string path = CopyFileToProgramFiles();

			AddUninstallSupport(path);
			CreateStartMenuShortcut(path);
			AddToPathVariable(path);
			AddFolderContextMenu();
			Log.Usage("Installed");
		}


		private void UninstallNormal()
		{
			Log.Usage("Uninstall normal");
			if (IsInstalledInstance())
			{
				// The running instance is the file, which should be deleted and would block deletion,
				// Copy the file to temp and run uninstallation from that file.
				string tempPath = CopyFileToTemp();
				Log.Debug("Start uninstaller in tmp folder");
				cmd.Start(tempPath, "/uninstall");
				return;
			}

			if (!Message.ShowAskOkCancel(
				$"Do you want to uninstall {Product.Name}?"))
			{
				return;
			}

			if (!EnsureNoOtherInstancesAreRunning())
			{
				return;
			}

			UninstallSilent();
			Log.Usage("Uninstalled normal");
			Message.ShowInfo($"Uninstallation of {Product.Name} is completed.");
		}


		private void UninstallSilent()
		{
			Log.Debug("Uninstalling...");
			DeleteProgramFilesFolder();
			DeleteProgramDataFolder();
			DeleteStartMenuShortcut();
			DeleteInPathVariable();
			DeleteFolderContextMenu();
			DeleteUninstallSupport();
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


		private static void DeleteProgramFilesFolder()
		{
			Thread.Sleep(300);
			string folderPath = ProgramInfo.GetProgramFolderPath();

			for (int i = 0; i < 5; i++)
			{
				try
				{
					if (Directory.Exists(folderPath))
					{
						Directory.Delete(folderPath, true);
					}
					else
					{
						return;
					}
				}
				catch (Exception e) when (e.IsNotFatal())
				{
					Log.Debug($"Failed to delete {folderPath}");
					Thread.Sleep(1000);
				}
			}
		}


		private static void DeleteProgramDataFolder()
		{
			string programDataFolderPath = ProgramInfo.GetProgramDataFolderPath();

			if (Directory.Exists(programDataFolderPath))
			{
				Directory.Delete(programDataFolderPath, true);
			}
		}



		private static void CreateStartMenuShortcut(string pathToExe)
		{
			string shortcutLocation = ProgramInfo.GetStartMenuShortcutPath();

			IWshRuntimeLibrary.WshShell shell = new IWshRuntimeLibrary.WshShell();
			IWshRuntimeLibrary.IWshShortcut shortcut = (IWshRuntimeLibrary.IWshShortcut)
				shell.CreateShortcut(shortcutLocation);

			shortcut.Description = Product.Name;
			shortcut.Arguments = "";

			shortcut.IconLocation = pathToExe;
			shortcut.TargetPath = pathToExe;
			shortcut.Save();
		}


		private static void DeleteStartMenuShortcut()
		{
			string shortcutLocation = ProgramInfo.GetStartMenuShortcutPath();
			File.Delete(shortcutLocation);
		}


		private static void AddToPathVariable(string path)
		{
			string folderPath = Path.GetDirectoryName(path);

			string keyName = @"Environment\";
			string pathsVariables = (string)Registry.CurrentUser.OpenSubKey(keyName)
				.GetValue("PATH", "", RegistryValueOptions.DoNotExpandEnvironmentNames);

			pathsVariables = pathsVariables.Trim();

			if (!pathsVariables.Contains(folderPath))
			{
				if (!string.IsNullOrEmpty(pathsVariables) && !pathsVariables.EndsWith(";"))
				{
					pathsVariables += ";";
				}

				pathsVariables += folderPath;
				Environment.SetEnvironmentVariable(
					"PATH", pathsVariables, EnvironmentVariableTarget.User);
			}
		}


		private static void DeleteInPathVariable()
		{
			string programFilesFolderPath = ProgramInfo.GetProgramFolderPath();

			string keyName = @"Environment\";
			string pathsVariables = (string)Registry.CurrentUser.OpenSubKey(keyName)
				.GetValue("PATH", "", RegistryValueOptions.DoNotExpandEnvironmentNames);

			string pathPart = programFilesFolderPath;
			if (pathsVariables.Contains(pathPart))
			{
				pathsVariables = pathsVariables.Replace(pathPart, "");
				pathsVariables = pathsVariables.Replace(";;", ";");
				pathsVariables = pathsVariables.Trim(";".ToCharArray());

				Registry.SetValue("HKEY_CURRENT_USER\\" + keyName, "PATH", pathsVariables);
			}
		}


		private static void AddUninstallSupport(string path)
		{
			string version = ProgramInfo.GetVersion(path).ToString();

			Registry.SetValue(UninstallRegKey, "DisplayName", Product.Name);
			Registry.SetValue(UninstallRegKey, "DisplayIcon", path);
			Registry.SetValue(UninstallRegKey, "Publisher", "Michael Reichenauer");
			Registry.SetValue(UninstallRegKey, "DisplayVersion", version);
			Registry.SetValue(UninstallRegKey, "UninstallString", path + " /uninstall");
			Registry.SetValue(UninstallRegKey, "EstimatedSize", 1000);
		}



		private static void DeleteUninstallSupport()
		{
			try
			{
				Registry.CurrentUser.DeleteSubKeyTree(UninstallSubKey);
			}
			catch (Exception e) when (e.IsNotFatal())
			{
				Log.Warn($"Failed to delete uninstall support {e}");
			}
		}


		private static void AddFolderContextMenu()
		{
			string programFilePath = ProgramInfo.GetInstallFilePath();

			Registry.SetValue(folderContextMenuPath, "", Product.Name);
			Registry.SetValue(folderContextMenuPath, "Icon", programFilePath);
			Registry.SetValue(folderCommandContextMenuPath, "", "\"" + programFilePath + "\" \"/d:%1\"");

			Registry.SetValue(directoryContextMenuPath, "", Product.Name);
			Registry.SetValue(directoryContextMenuPath, "Icon", programFilePath);
			Registry.SetValue(
				directoryCommandContextMenuPath, "", "\"" + programFilePath + "\" \"/d:%V\"");

			Registry.SetValue(dllContextMenuPath, "", Product.Name);
			Registry.SetValue(dllContextMenuPath, "Icon", programFilePath);
			Registry.SetValue(dllCommandContextMenuPath, "", "\"" + programFilePath + "\" \"%1\"");

		}


		private static void DeleteFolderContextMenu()
		{
			try
			{
				Registry.CurrentUser.DeleteSubKeyTree(subFolderContextMenuPath);
				Registry.CurrentUser.DeleteSubKeyTree(subDirectoryBackgroundContextMenuPath);
				Registry.CurrentUser.DeleteSubKeyTree(subdllContextMenuPath);
			}
			catch (Exception e) when (e.IsNotFatal())
			{
				Log.Warn($"Failed to delete folder context menu {e}");
			}

		}


		private static bool IsInstalledInstance()
		{
			string folderPath = Path.GetDirectoryName(ProgramInfo.GetCurrentInstancePath());
			string programFolderDependinator = ProgramInfo.GetProgramFolderPath();

			return folderPath == programFolderDependinator;
		}


		private static string CopyFileToTemp()
		{
			string sourcePath = ProgramInfo.GetCurrentInstancePath();
			string targetPath = Path.Combine(Path.GetTempPath(), ProgramInfo.ProgramFileName);
			File.Copy(sourcePath, targetPath, true);

			return targetPath;
		}
	}
}