using System;
using System.IO;
using Dependinator.Common.Private;
using Dependinator.Utils;

namespace Dependinator.Common.WorkFolders
{
	[SingleInstance]
	internal class WorkingFolder
	{
		private readonly IWorkingFolderService workingFolderService;

		
		private string FolderPath => workingFolderService.FolderPath;

		public WorkingFolder(IWorkingFolderService workingFolderService)
		{
			this.workingFolderService = workingFolderService;
		}


		public event EventHandler OnChange
		{
			add => workingFolderService.OnChange += value;
			remove => workingFolderService.OnChange -= value;
		}

		public string FilePath => workingFolderService.FilePath;

		public bool IsValid => FilePath != null && File.Exists(FilePath);

		public bool HasValue => FolderPath != null;

		public string Name => HasValue ? Path.GetFileNameWithoutExtension(FilePath) : null;

		public static implicit operator string(WorkingFolder workingFolder) => workingFolder.FolderPath;


		public bool TrySetPath(string path)
		{
			return workingFolderService.TrySetPath(path);
		}


		public override string ToString() => FolderPath;
	}
}