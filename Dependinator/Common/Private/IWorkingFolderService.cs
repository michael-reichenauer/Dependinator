using System;

namespace Dependinator.Common.Private
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