using System;


namespace Dependinator.Common.WorkFolders.Private
{
	internal interface IWorkingFolderService
	{
		event EventHandler OnChange;
		string FolderPath { get; }
		string FilePath { get; }

		bool TrySetPath(string path);
	}
}