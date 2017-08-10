using System;

namespace Dependinator.Common.FolderMonitoring
{
	internal interface IFolderMonitorService
	{
		event EventHandler<FileEventArgs> FileChanged;

		event EventHandler<FileEventArgs> RepoChanged;

		void Monitor(string workingFolder);
	}
}