using System;
using System.IO;
using System.Threading;
using System.Windows;
using Dependinator.Common.Environment;
using Dependinator.Common.MessageDialogs;
using Dependinator.Common.SettingsHandling;
using Dependinator.Utils;


namespace Dependinator.Common.Installation.Private
{
	internal class Installer : IInstaller
	{
		private readonly ICommandLine commandLine;

		//private static readonly string UninstallSubKey =
		//	$"SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\Uninstall\\{{{Product.Guid}}}_is1";

		//private static readonly string UninstallRegKey = "HKEY_CURRENT_USER\\" + UninstallSubKey;
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
				null,
				$"Welcome to the {Product.Name} setup.\n\n" +
				"This will:\n" +
				$" - Copy {Product.Name} to the program data folder.\n" +
				$" - Add a {Product.Name} shortcut in the Start Menu.\n" +
				$"\n\nClick OK to install {Product.Name} or Cancel to exit setup.",
				SetupTitle,
				MessageBoxImage.Information))
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

			// AddUninstallSupport(path);
			//CreateStartMenuShortcut(path);
			//ExtractExampleModel();

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
			//DeleteProgramFilesFolder();
			//DeleteProgramDataFolder();
			// DeleteStartMenuShortcut();
			//DeleteUninstallSupport();
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


		//private static void DeleteProgramFilesFolder()
		//{
		//	Thread.Sleep(300);
		//	string folderPath = ProgramInfo.GetProgramFolderPath();

		//	for (int i = 0; i < 5; i++)
		//	{
		//		try
		//		{
		//			if (Directory.Exists(folderPath))
		//			{
		//				Directory.Delete(folderPath, true);
		//			}
		//			else
		//			{
		//				return;
		//			}
		//		}
		//		catch (Exception e) when (e.IsNotFatal())
		//		{
		//			Log.Debug($"Failed to delete {folderPath}");
		//			Thread.Sleep(1000);
		//		}
		//	}
		//}


		//private static void DeleteProgramDataFolder()
		//{
		//	string programDataFolderPath = ProgramInfo.GetProgramDataFolderPath();

		//	if (Directory.Exists(programDataFolderPath))
		//	{
		//		Directory.Delete(programDataFolderPath, true);
		//	}
		//}


		//private static void CreateStartMenuShortcut(string pathToExe)
		//{
		//	string shortcutLocation = ProgramInfo.GetStartMenuShortcutPath();

		//	IWshRuntimeLibrary.WshShell shell = new IWshRuntimeLibrary.WshShell();
		//	IWshRuntimeLibrary.IWshShortcut shortcut = (IWshRuntimeLibrary.IWshShortcut)
		//		shell.CreateShortcut(shortcutLocation);

		//	shortcut.Description = Product.Name;
		//	shortcut.Arguments = "";

		//	shortcut.IconLocation = pathToExe;
		//	shortcut.TargetPath = pathToExe;
		//	shortcut.Save();
		//}


		//private static void DeleteStartMenuShortcut()
		//{
		//	string shortcutLocation = ProgramInfo.GetStartMenuShortcutPath();
		//	File.Delete(shortcutLocation);
		//}


		//private static void AddUninstallSupport(string path)
		//{
		//	string version = ProgramInfo.GetVersion(path).ToString();

		//	Registry.SetValue(UninstallRegKey, "DisplayName", Product.Name);
		//	Registry.SetValue(UninstallRegKey, "DisplayIcon", path);
		//	Registry.SetValue(UninstallRegKey, "Publisher", "Michael Reichenauer");
		//	Registry.SetValue(UninstallRegKey, "DisplayVersion", version);
		//	Registry.SetValue(UninstallRegKey, "UninstallString", path + " /uninstall");
		//	Registry.SetValue(UninstallRegKey, "EstimatedSize", 1000);
		//}


		//private static void DeleteUninstallSupport()
		//{
		//	try
		//	{
		//		Registry.CurrentUser.DeleteSubKeyTree(UninstallSubKey);
		//	}
		//	catch (Exception e) when (e.IsNotFatal())
		//	{
		//		Log.Warn($"Failed to delete uninstall support {e}");
		//	}
		//}


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

		//private void ExtractExampleModel()
		//{
		//	string dataFolderPath = ProgramInfo.GetProgramDataFolderPath();
		//	string exampleFolderPath = Path.Combine(dataFolderPath, "Example");
		//	string examplePath = Path.Combine(exampleFolderPath, "Example.exe");
		//	string exampleXmlPath = Path.Combine(exampleFolderPath, "Example.xml");

		//	if (!Directory.Exists(exampleFolderPath))
		//	{
		//		Directory.CreateDirectory(exampleFolderPath);
		//	}

		//	try
		//	{
		//		if (File.Exists(examplePath))
		//		{
		//			File.Delete(examplePath);
		//		}

		//		if (File.Exists(ProgramInfo.GetInstallFilePath()))
		//		{
		//			File.Copy(ProgramInfo.GetInstallFilePath(), examplePath);
		//		}
		//		else
		//		{
		//			File.Copy(Assembly.GetEntryAssembly().Location, examplePath);
		//		}

		//		Assembly executingAssembly = Assembly.GetExecutingAssembly();
		//		string resourceName = $"{Product.Name}.Common.Resources.Example.xml";
		//		using (Stream stream = executingAssembly.GetManifestResourceStream(resourceName))
		//		{
		//			StreamReader sr = new StreamReader(stream);
		//			string text = sr.ReadToEnd();
		//			File.WriteAllText(exampleXmlPath, text);
		//		}
		//	}
		//	catch (Exception e)
		//	{
		//		Log.Exception(e, "Failed to copy example model");
		//	}
		//}
	}
}