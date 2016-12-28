using System;


namespace Dependiator.ApplicationHandling
{
	internal interface IWorkingFolderService
	{
		event EventHandler OnChange;
		string Path { get; }
		bool IsValid { get; }

		bool TrySetPath(string path);
	}
}