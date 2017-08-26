using System;
using System.IO;
using Dependinator.ApplicationHandling.SettingsHandling;
using Dependinator.Utils;


namespace Dependinator.ApplicationHandling.Private
{
	[SingleInstance]
	internal class WorkingFolderService : IWorkingFolderService
	{
		private readonly ICommandLine commandLine;

		private string workingFolder;


		public WorkingFolderService(ICommandLine commandLine)
		{
			this.commandLine = commandLine;
		}


		public bool IsValid { get; private set; }


		public event EventHandler OnChange;

		public string Path
		{
			get
			{
				if (workingFolder == null)
				{
					workingFolder = GetInitialWorkingFolder();
					StoreLastedUsedFolder();
				}

				return workingFolder;
			}
		}

		public string FilePath { get; private set; }


		public bool TrySetPath(string path)
		{
			if (GetWorkingFolderPath(path).HasValue(out string rootFolder))
			{
				if (workingFolder != rootFolder)
				{
					workingFolder = rootFolder;
					StoreLastedUsedFolder();
					OnChange?.Invoke(this, EventArgs.Empty);
				}

				IsValid = true;
				return true;
			}
			else
			{
				return false;
			}
		}



		private void StoreLastedUsedFolder()
		{
			if (IsValid)
			{
				Settings.Edit<ProgramSettings>(settings => settings.LastUsedWorkingFolder = workingFolder);
			}
		}


		// Must be able to handle:
		// * Starting app from start menu or pinned (no parameters and unknown current dir)
		// * Starting on command line in some dir (no parameters but known dir)
		// * Starting as right click on folder (parameter "/d:<dir>"
		// * Starting on command line with some parameters (branch names)
		// * Starting with parameters "/test"
		private string GetInitialWorkingFolder()
		{
			R<string> folderPath = R<string>.NoValue;
			if (commandLine.HasFile)
			{
				// Call from e.g. Windows Explorer file context menu
				folderPath = GetWorkingFolderPath(commandLine.FilePath);
				IsValid = folderPath.IsOk;
				return folderPath.IsOk ? folderPath.Value : commandLine.FilePath;
			}

			//string lastUsedFolder = GetLastUsedWorkingFolder();
			//if (!string.IsNullOrWhiteSpace(lastUsedFolder))
			//{
			//	folderPath = lastUsedFolder;
			//}
			//else
			//{
			//	folderPath = GetWorkingFolderPath(Assembly.GetEntryAssembly().Location);
			//}

			if (!ProgramPaths.IsInstalledInstance())
			{
				folderPath = GetWorkingFolderPath(ProgramPaths.GetCurrentInstancePath());
				IsValid = folderPath.IsOk;
				return folderPath.IsOk ? folderPath.Value : commandLine.FilePath;
			}


			folderPath = GetWorkingFolderPath("Default");

			IsValid = folderPath.IsOk;
			if (folderPath.IsOk)
			{
				FilePath = Settings.Get<WorkFolderSettings>().FilePath;
				return folderPath.Value;
			}

			IsValid = false;
			return GetMyDocumentsPath();
		}


		private static string GetLastUsedWorkingFolder()
		{
			return Settings.Get<ProgramSettings>().LastUsedWorkingFolder;
		}

		private static string GetMyDocumentsPath()
		{
			return Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
		}



		public R<string> GetWorkingFolderPath(string path)
		{
			if (!IsValidPath(path))
			{
				return Error.From("No valid file");
			}

			string folderPath = ProgramPaths.GetWorkingFolderPath(path);

			if (0 != Txt.CompareIc(path, workingFolder))
			{
				Settings.Edit<WorkFolderSettings>(folderPath, settings => settings.FilePath = path);

				FilePath = path;
			}

			FilePath = Settings.Get<WorkFolderSettings>(folderPath).FilePath;
			return folderPath;
		}


		private static bool IsValidPath(string path)
		{
			if (path == null || !File.Exists(path))
			{
				return false;
			}

			if (!(path.EndsWith(".dll") || path.EndsWith(".exe")))
			{
				return false;
			}

			return true;
		}
	}
}