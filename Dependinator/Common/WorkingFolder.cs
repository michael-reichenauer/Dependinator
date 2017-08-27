using System;
using Dependinator.Common.Private;
using Dependinator.Utils;

namespace Dependinator.Common
{
	[SingleInstance]
	internal class WorkingFolder
	{
		private readonly IWorkingFolderService workingFolderService;


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

		public bool IsValid => workingFolderService.IsValid;

		public bool HasValue => workingFolderService.Path != null;

		public string Name => HasValue ? System.IO.Path.GetFileNameWithoutExtension(FilePath) : null;

		public static implicit operator string(WorkingFolder workingFolder) =>
			workingFolder.workingFolderService.Path;


		public bool TrySetPath(string path)
		{
			return workingFolderService.TrySetPath(path);
		}


		public override string ToString() => workingFolderService.Path;
	}
}