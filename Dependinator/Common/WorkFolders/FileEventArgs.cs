using System;

namespace Dependinator.Common.WorkFolders
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