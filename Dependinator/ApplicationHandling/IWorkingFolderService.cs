using System;


namespace Dependinator.ApplicationHandling
{
	internal interface IWorkingFolderService
	{
		event EventHandler OnChange;
		string Path { get; }
		string FilePath { get; }
		bool IsValid { get; }

		bool TrySetPath(string path);
	}
}