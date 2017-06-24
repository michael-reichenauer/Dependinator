using System;


namespace Dependinator.Features.FolderMonitoring
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