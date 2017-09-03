using System;
using System.IO;
using Dependinator.Common.SettingsHandling;
using Dependinator.Utils;

namespace Dependinator.Common.WorkFolders.Private
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

		
		public event EventHandler OnChange;

		public string FilePath { get; private set; }


		public string FolderPath => workingFolder ?? (workingFolder = GetInitialWorkingFolder());


		public bool TrySetPath(string path)
		{
			if (TryGetWorkingFolderPath(path, out string folder))
			{
				if (0 != Txt.CompareIc(workingFolder, folder))
				{
					workingFolder = folder;
					FilePath = path;
					OnChange?.Invoke(this, EventArgs.Empty);
				}

				return true;
			}

			return false;
		}



		private string GetInitialWorkingFolder()
		{
			if (commandLine.HasFile && TryGetWorkingFolderPath(commandLine.FilePath, out string folder))
			{
				// Call from e.g. Windows Explorer file context menu
				workingFolder = folder;
				FilePath = commandLine.FilePath;
				return workingFolder;
			}


			//if (!ProgramInfo.IsInstalledInstance()
			//	&& TryGetWorkingFolderPath(ProgramInfo.GetCurrentInstancePath(), out folder))
			//{
			//	workingFolder = folder;
			//	FilePath = ProgramInfo.GetCurrentInstancePath();
			//	return workingFolder;
			//}


			FilePath = null;
			return "";
		}



		public bool TryGetWorkingFolderPath(string filePath, out string folderPath)
		{
			if (!IsValidPath(filePath))
			{
				folderPath = null;
				return false;
			}

			folderPath = ProgramInfo.GetWorkingFolderPath(filePath);
	
			return true;
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