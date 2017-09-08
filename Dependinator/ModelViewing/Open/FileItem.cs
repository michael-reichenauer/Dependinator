using System;
using Dependinator.Utils.UI;
using Dependinator.Utils.UI.Mvvm;


namespace Dependinator.ModelViewing.Open
{
	internal class FileItem : ViewModel
	{
		private readonly Action<string> openFileAction;

		public FileItem(string fileName, string filePath, Action<string> openFileAction)
		{
			FileName = fileName;
			FilePath = filePath;
			this.openFileAction = openFileAction;
		}


		public string FilePath { get; }
		public string FileName { get; }
		public string ToolTip => "Show model for " + FilePath;
		public Command OpenFileCommand => Command(() => openFileAction(FilePath));
	}
}