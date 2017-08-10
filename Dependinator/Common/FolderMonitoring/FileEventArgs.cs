using System;

namespace Dependinator.Common.FolderMonitoring
{
	public class FileEventArgs : EventArgs
	{
		public DateTime DateTime { get; }

		public FileEventArgs(DateTime dateTime)
		{
			DateTime = dateTime;
		}
	}
}